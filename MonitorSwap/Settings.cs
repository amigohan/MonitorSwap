using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;
using static MonitorSwap.Native;

namespace MonitorSwap;

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

    public string Theme { get; set; } = "Koyu";      // "Koyu" ya da "Macera" (ayarlardan açılır)
    public bool SoundsEnabled { get; set; } = true;
    public int SoundVolume { get; set; } = 70;         // 0–100
    public bool ShowSplash { get; set; } = true;

    public Hotkey HotkeyPc { get; set; } = Hotkey.Of(CAS, Keys.D1);
    public Hotkey HotkeySwitch { get; set; } = Hotkey.Of(CAS, Keys.D2);
    public Hotkey HotkeyToggle { get; set; } = Hotkey.Of(CAS, Keys.D0);
    public Hotkey HotkeySounds { get; set; } = Hotkey.Of(CAS, Keys.M);

    // ---------------- Kaydetme / yükleme ----------------

    static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MonitorSwap");
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

    /// <summary>
    /// Uygulamanın eski adı "EkranGeçiş" idi. Eski sürümden gelenlerin ayarları, kayıt dosyası ve
    /// "Windows ile başlat" kaydı yeni ada (Monitor Swap) bir kez taşınır.
    /// </summary>
    public static void MigrateFromOldName()
    {
        try
        {
            string oldDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EkranGecis");
            if (Directory.Exists(oldDir) && !Directory.Exists(Dir))
            {
                Directory.CreateDirectory(Dir);
                foreach (var f in Directory.GetFiles(oldDir))
                    File.Copy(f, Path.Combine(Dir, Path.GetFileName(f)), overwrite: false);
            }
        }
        catch { }

        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (k?.GetValue("EkranGecis") is string)
            {
                k.DeleteValue("EkranGecis", false);
                k.SetValue(RunName, "\"" + Environment.ProcessPath + "\" --autostart");
            }
        }
        catch { }
    }

    public AppSettings Clone() =>
        JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(this, Json), Json);

    // ---------------- Windows ile başlat ----------------

    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string RunName = "MonitorSwap";

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
