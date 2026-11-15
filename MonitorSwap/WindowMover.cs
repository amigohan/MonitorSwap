using System.Runtime.InteropServices;
using System.Text;
using static MonitorSwap.Native;

namespace MonitorSwap;

/// <summary>
/// Switch moduna geçerken ana monitördeki pencereleri laptop ekranına taşır,
/// bilgisayara dönünce eski yerlerine koyar.
/// </summary>
static class WindowMover
{
    static readonly List<(IntPtr Hwnd, RECT NormalWs)> Saved = new();

    static readonly HashSet<string> SkipClasses = new()
    {
        "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd",
        "Windows.UI.Core.CoreWindow", "XamlExplorerHostIslandWindow",
        "TopLevelWindowForOverflowXamlIsland", "NotifyIconOverflowWindow",
    };

    public static int MovedCount => Saved.Count;

    /// <summary>Taşınabilir üst düzey pencereler; en alttakinden en üsttekine doğru (z-sırası korunsun diye).</summary>
    static List<IntPtr> Candidates()
    {
        uint myPid = (uint)Environment.ProcessId;
        var list = new List<IntPtr>();
        var cls = new StringBuilder(256);
        EnumWindows((h, _) =>
        {
            if (!IsWindowVisible(h)) return true;
            GetWindowThreadProcessId(h, out uint pid);
            if (pid == myPid) return true;
            long ex = GetWindowLongPtr(h, GWL_EXSTYLE).ToInt64();
            if ((ex & WS_EX_TOOLWINDOW) != 0 && (ex & WS_EX_APPWINDOW) == 0) return true;
            if ((ex & WS_EX_NOACTIVATE) != 0 && (ex & WS_EX_APPWINDOW) == 0) return true;
            if (DwmGetWindowAttribute(h, DWMWA_CLOAKED, out int cloaked, 4) == 0 && cloaked != 0) return true;
            if (GetWindowTextLength(h) == 0) return true;
            cls.Clear();
            GetClassName(h, cls, cls.Capacity);
            if (SkipClasses.Contains(cls.ToString())) return true;
            list.Add(h);
            return true;
        }, IntPtr.Zero);
        list.Reverse();
        return list;
    }

    /// <summary>GetWindowPlacement "çalışma alanı" koordinatı kullanır; ekran koordinatına çevirme farkı.</summary>
    static (int X, int Y) WorkspaceOffset()
    {
        var mi = MonitorInfo(MonitorFromPoint(new POINT(), MONITOR_DEFAULTTOPRIMARY));
        return (mi.rcWork.Left - mi.rcMonitor.Left, mi.rcWork.Top - mi.rcMonitor.Top);
    }

    static IntPtr MonitorOf(IntPtr h, RECT normalScreen)
        => IsIconic(h) ? MonitorFromRect(ref normalScreen, MONITOR_DEFAULTTONEAREST)
                       : MonitorFromWindow(h, MONITOR_DEFAULTTONEAREST);

    /// <summary>Kaynak çalışma alanındaki konumu oransal olarak hedefe taşır, sığmazsa küçültür.</summary>
    static RECT Fit(RECT r, RECT src, RECT dst)
    {
        int w = Math.Min(r.Width, dst.Width), h = Math.Min(r.Height, dst.Height);
        double fx = src.Width > r.Width ? (double)(r.Left - src.Left) / (src.Width - r.Width) : 0;
        double fy = src.Height > r.Height ? (double)(r.Top - src.Top) / (src.Height - r.Height) : 0;
        fx = Math.Clamp(fx, 0, 1);
        fy = Math.Clamp(fy, 0, 1);
        int x = dst.Left + (int)Math.Round(fx * (dst.Width - w));
        int y = dst.Top + (int)Math.Round(fy * (dst.Height - h));
        return new RECT(x, y, x + w, y + h);
    }

    /// <summary>Pencerenin durumunu (simge/tam ekran/normal) koruyarak normal konumunu değiştirir.</summary>
    static bool Place(IntPtr h, RECT normalWs)
    {
        var wp = new WINDOWPLACEMENT { length = Marshal.SizeOf<WINDOWPLACEMENT>() };
        if (!GetWindowPlacement(h, ref wp)) return false;
        var np = wp;
        np.rcNormalPosition = normalWs;
        np.flags = wp.flags & WPF_RESTORETOMAXIMIZED;

        if (IsIconic(h))
        {
            np.showCmd = SW_SHOWMINNOACTIVE;
            return SetWindowPlacement(h, ref np);
        }
        if (IsZoomed(h))
        {
            // Tam ekran pencere önce yeni yere "normal" olarak alınır, sonra tekrar büyütülür.
            np.showCmd = SW_SHOWNOACTIVATE;
            bool ok = SetWindowPlacement(h, ref np);
            ShowWindow(h, SW_MAXIMIZE);
            return ok;
        }
        np.showCmd = SW_SHOWNOACTIVATE;
        return SetWindowPlacement(h, ref np);
    }

    /// <summary>DPI farkı yüzünden büyüyen pencereleri hedef ekrana sığdırır.</summary>
    static void ClampInto(IEnumerable<IntPtr> windows, RECT area)
    {
        foreach (var h in windows)
        {
            if (!IsWindow(h) || IsIconic(h) || IsZoomed(h)) continue;
            if (!GetWindowRect(h, out var r)) continue;
            bool inside = r.Left >= area.Left - 16 && r.Top >= area.Top - 16 &&
                          r.Right <= area.Right + 16 && r.Bottom <= area.Bottom + 16;
            if (inside) continue;
            var f = Fit(r, area, area);
            int x = Math.Clamp(r.Left, area.Left, area.Right - f.Width);
            int y = Math.Clamp(r.Top, area.Top, area.Bottom - f.Height);
            SetWindowPos(h, IntPtr.Zero, x, y, f.Width, f.Height, SWP_NOZORDER | SWP_NOACTIVATE);
        }
    }

    public static int MoveAll(IntPtr fromMon, IntPtr toMon)
    {
        Saved.Clear();
        if (fromMon == IntPtr.Zero || toMon == IntPtr.Zero || fromMon == toMon) return 0;
        var src = MonitorInfo(fromMon).rcWork;
        var dst = MonitorInfo(toMon).rcWork;
        var (ox, oy) = WorkspaceOffset();
        var normals = new List<IntPtr>();

        foreach (var h in Candidates())
        {
            try
            {
                if (IsHungAppWindow(h)) continue;
                var wp = new WINDOWPLACEMENT { length = Marshal.SizeOf<WINDOWPLACEMENT>() };
                if (!GetWindowPlacement(h, ref wp)) continue;
                var normalScreen = wp.rcNormalPosition.Offset(ox, oy);
                if (MonitorOf(h, normalScreen) != fromMon) continue;

                var target = Fit(normalScreen, src, dst).Offset(-ox, -oy);
                bool wasNormal = !IsIconic(h) && !IsZoomed(h);
                if (Place(h, target))
                {
                    Saved.Add((h, wp.rcNormalPosition));
                    if (wasNormal) normals.Add(h);
                }
            }
            catch { /* yönetici olarak çalışan pencereler vb. taşınamayabilir */ }
        }

        Thread.Sleep(300);
        ClampInto(normals, dst);
        return Saved.Count;
    }

    /// <summary>Taşınan pencereleri eski yerlerine koyar (kullanıcının bu arada başka yere taşıdıkları hariç).</summary>
    public static int RestoreAll(IntPtr laptopMon)
    {
        int n = 0;
        var (ox, oy) = WorkspaceOffset();
        foreach (var (h, normalWs) in Saved)
        {
            try
            {
                if (!IsWindow(h) || IsHungAppWindow(h)) continue;
                var wp = new WINDOWPLACEMENT { length = Marshal.SizeOf<WINDOWPLACEMENT>() };
                if (!GetWindowPlacement(h, ref wp)) continue;
                if (laptopMon != IntPtr.Zero && MonitorOf(h, wp.rcNormalPosition.Offset(ox, oy)) != laptopMon) continue;
                if (Place(h, normalWs)) n++;
            }
            catch { }
        }
        Saved.Clear();
        return n;
    }
}
