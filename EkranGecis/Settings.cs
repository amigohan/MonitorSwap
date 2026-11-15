using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;
using static EkranGecis.Native;

namespace EkranGecis;

sealed class Hotkey
{
    public uint Mods { get; set; }
    public int Key { get; set; }

    [JsonIgnore] public bool IsEmpty => Key == 0;

    public static Hotkey Of(uint mods, Keys key) => new() { Mods = mods, Key = (int)key };

    public override string ToString()
    {
        if (IsEmpty) return "(yok)";
        var parts = new List<string>();
        if ((Mods & MOD_CONTROL) != 0) parts.Add("Ctrl");
        if ((Mods & MOD_ALT) != 0) parts.Add("Alt");
        if ((Mods & MOD_SHIFT) != 0) parts.Add("Shift");
        if ((Mods & MOD_WIN) != 0) parts.Add("Win");
        parts.Add(KeyName((Keys)Key));
        return string.Join("+", parts);
    }

    static string KeyName(Keys k)
    {
        if (k >= Keys.D0 && k <= Keys.D9) return ((char)('0' + (k - Keys.D0))).ToString();
        if (k >= Keys.NumPad0 && k <= Keys.NumPad9) return "Num" + (k - Keys.NumPad0);
        return k.ToString();
    }
}

sealed class AppSettings
{
    const uint CAS = MOD_CONTROL | MOD_ALT | MOD_SHIFT;

    public uint PcInput { get; set; } = 0x0F;      // DisplayPort
    public uint SwitchInput { get; set; } = 0x11;  // HDMI-1
    public string DeviceName { get; set; } = "Nintendo Switch";

    public bool ChangeAudio { get; set; } = false;
    public string SwitchAudioId { get; set; } = "";
    public string SwitchAudioName { get; set; } = "";
    public bool RestoreAudio { get; set; } = true;

    public bool MoveWindows { get; set; } = true;
    public bool ShowMiniPanel { get; set; } = true;

    public Hotkey HotkeyPc { get; set; } = Hotkey.Of(CAS, Keys.D1);
    public Hotkey HotkeySwitch { get; set; } = Hotkey.Of(CAS, Keys.D2);
    public Hotkey HotkeyToggle { get; set; } = Hotkey.Of(CAS, Keys.D0);

    // ---------------- Kaydetme / yükleme ----------------

    static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EkranGecis");
    static readonly string FilePath = Path.Combine(Dir, "ayarlar.json");
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static bool Exists => File.Exists(FilePath);

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Json) ?? new AppSettings();
        }
        catch { }
        return new AppSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Json));
    }

    public AppSettings Clone() =>
        JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(this, Json), Json);

    // ---------------- Windows ile başlat ----------------

    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string RunName = "EkranGecis";

    public static bool Autostart
    {
        get
        {
            using var k = Registry.CurrentUser.OpenSubKey(RunKey);
            return k?.GetValue(RunName) is string;
        }
        set
        {
            using var k = Registry.CurrentUser.CreateSubKey(RunKey);
            if (value) k.SetValue(RunName, "\"" + Environment.ProcessPath + "\" --autostart");
            else k.DeleteValue(RunName, false);
        }
    }
}
