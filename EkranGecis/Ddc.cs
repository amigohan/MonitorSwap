using System.Text;
using static EkranGecis.Native;

namespace EkranGecis;

/// <summary>
/// Monitöre DDC/CI üzerinden komut gönderir. VCP 0x60 = giriş kaynağı.
/// Tüm çağrılar tek bir kilit altında sıralanır (I2C hattında çakışma olmasın diye).
/// </summary>
static class Ddc
{
    const byte VcpInput = 0x60;

    /// <summary>Monitör desteklediği girişleri bildirmezse gösterilecek varsayılanlar.</summary>
    public static readonly uint[] DefaultInputs = { 0x0F, 0x11, 0x12 };

    // MCCS standardındaki giriş kodları (+ bazı üreticilerin USB-C için kullandığı 0x1B).
    static readonly Dictionary<uint, string> Names = new()
    {
        [0x01] = "VGA-1", [0x02] = "VGA-2", [0x03] = "DVI-1", [0x04] = "DVI-2",
        [0x05] = "Composite-1", [0x06] = "Composite-2", [0x07] = "S-Video-1", [0x08] = "S-Video-2",
        [0x0C] = "Component-1", [0x0D] = "Component-2", [0x0E] = "Component-3",
        [0x0F] = "DisplayPort", [0x10] = "DisplayPort-2", [0x11] = "HDMI-1", [0x12] = "HDMI-2",
        [0x13] = "HDMI-3", [0x1B] = "USB-C",
    };

    public static string InputName(uint code) =>
        Names.TryGetValue(code, out var n) ? n : "Giriş 0x" + code.ToString("X2");

    static readonly object Gate = new();

    /// <summary>DDC/CI'ya cevap veren monitörün Windows aygıt adı (ör. \\.\DISPLAY2).</summary>
    public static string LastDevice { get; private set; }

    sealed class Target : IDisposable
    {
        public PHYSICAL_MONITOR[] All;
        public IntPtr Handle;
        public void Dispose()
        {
            if (All != null) DestroyPhysicalMonitors((uint)All.Length, All);
            All = null;
        }
    }

    static Target Find()
    {
        var monitors = EnumMonitors();
        // Daha önce bulunan monitörü önce dene (laptop panelini her seferinde sorgulamamak için).
        monitors.Sort((a, b) => (b.Device == LastDevice).CompareTo(a.Device == LastDevice));

        foreach (var (hmon, device) in monitors)
        {
            if (!GetNumberOfPhysicalMonitorsFromHMONITOR(hmon, out uint n) || n == 0) continue;
            var arr = new PHYSICAL_MONITOR[n];
            if (!GetPhysicalMonitorsFromHMONITOR(hmon, n, arr)) continue;

            int tries = device == LastDevice ? 3 : 1;
            foreach (var p in arr)
            {
                for (int t = 0; t < tries; t++)
                {
                    if (GetVCPFeatureAndVCPFeatureReply(p.hPhysicalMonitor, VcpInput, IntPtr.Zero, out _, out _))
                    {
                        LastDevice = device;
                        return new Target { All = arr, Handle = p.hPhysicalMonitor };
                    }
                    Thread.Sleep(80);
                }
            }
            DestroyPhysicalMonitors(n, arr);
        }
        return null;
    }

    public static bool TryReadInput(out uint value)
    {
        value = 0;
        lock (Gate)
        {
            using var t = Find();
            if (t == null) return false;
            for (int i = 0; i < 3; i++)
            {
                if (GetVCPFeatureAndVCPFeatureReply(t.Handle, VcpInput, IntPtr.Zero, out value, out _)) return true;
                Thread.Sleep(80);
            }
            return false;
        }
    }

    public static bool SetInput(uint value)
    {
        // Monitör o an başka bir sorguya cevap veriyorsa bulunamayabilir; birkaç kez dene.
        for (int attempt = 0; attempt < 4; attempt++)
        {
            lock (Gate)
            {
                using var t = Find();
                if (t != null)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        if (SetVCPFeature(t.Handle, VcpInput, value)) return true;
                        Thread.Sleep(150);
                    }
                }
            }
            Thread.Sleep(400);
        }
        return false;
    }

    /// <summary>Monitörün bildirdiği model adı.</summary>
    public static string ReadModel() => ReadCapabilities().Model;

    /// <summary>
    /// Monitörün yetenek bilgisinden model adını ve desteklediği girişleri okur.
    /// Ör. "...model(XYZ)...vcp(... 60(0F 11 12) ...)" → ("XYZ", [0x0F, 0x11, 0x12]).
    /// </summary>
    public static (string Model, List<uint> Inputs) ReadCapabilities()
    {
        string caps;
        lock (Gate)
        {
            using var t = Find();
            if (t == null) return (null, null);
            if (!GetCapabilitiesStringLength(t.Handle, out uint len) || len == 0) return (null, null);
            var sb = new StringBuilder((int)len);
            if (!CapabilitiesRequestAndCapabilitiesReply(t.Handle, sb, len)) return (null, null);
            caps = sb.ToString();
        }

        string model = null;
        var m = System.Text.RegularExpressions.Regex.Match(caps, @"model\(([^)]*)\)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (m.Success) model = m.Groups[1].Value.Trim();

        List<uint> inputs = null;
        var v = System.Text.RegularExpressions.Regex.Match(caps, @"(?<![0-9A-Fa-f])60\(([0-9A-Fa-f ]*)\)");
        if (v.Success)
        {
            string raw = v.Groups[1].Value.Replace(" ", "");
            inputs = new List<uint>();
            for (int i = 0; i + 1 < raw.Length; i += 2)
                inputs.Add(Convert.ToUInt32(raw.Substring(i, 2), 16));
            if (inputs.Count == 0) inputs = null;
        }
        return (model, inputs);
    }

    /// <summary>DDC monitörünün ve "diğer" ekranın (laptop) HMONITOR'larını döndürür.</summary>
    public static (IntPtr Main, IntPtr Other, string OtherDevice) Layout()
    {
        IntPtr main = IntPtr.Zero, other = IntPtr.Zero;
        string otherDevice = null;
        foreach (var (h, dev) in EnumMonitors())
        {
            if (dev == LastDevice) main = h;
            else if (other == IntPtr.Zero) { other = h; otherDevice = dev; }
        }
        return (main, other, otherDevice);
    }
}
