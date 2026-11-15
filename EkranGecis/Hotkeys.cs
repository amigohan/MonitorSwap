using static EkranGecis.Native;

namespace EkranGecis;

/// <summary>Sistem genelinde kısayol tuşlarını dinleyen gizli pencere.</summary>
sealed class HotkeyManager : NativeWindow, IDisposable
{
    public event Action<int> Pressed;
    readonly List<int> ids = new();

    public HotkeyManager() => CreateHandle(new CreateParams());

    public bool Register(int id, Hotkey hk)
    {
        if (hk == null || hk.IsEmpty) return true;
        bool ok = RegisterHotKey(Handle, id, hk.Mods | MOD_NOREPEAT, (uint)hk.Key);
        if (ok) ids.Add(id);
        return ok;
    }

    public void UnregisterAll()
    {
        foreach (var id in ids) UnregisterHotKey(Handle, id);
        ids.Clear();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY) Pressed?.Invoke(m.WParam.ToInt32());
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        UnregisterAll();
        DestroyHandle();
    }
}
