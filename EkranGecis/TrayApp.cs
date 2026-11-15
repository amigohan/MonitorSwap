using System.Reflection;

namespace EkranGecis;

enum Mode { Unknown, Pc, Switch }

/// <summary>Uygulamanın ana mantığı: tepsi simgesi, kısayollar, mod geçişleri ve izleme.</summary>
sealed class TrayApp : ApplicationContext
{
    const int HkPc = 1, HkSwitch = 2, HkToggle = 3, HkSounds = 4;
    const uint NoValue = uint.MaxValue;

    AppSettings settings;
    readonly NotifyIcon tray;
    readonly ContextMenuStrip menu = Menus.Create();
    readonly ToolStripMenuItem statusItem, pcItem, swItem, soundsItem;
    readonly HotkeyManager hotkeys = new();
    MiniPanel panel;
    Osd osd;
    readonly Control ui = new();
    readonly System.Windows.Forms.Timer poll = new() { Interval = 3000 };
    Icon iconPc, iconSw, iconUnknown;

    Mode mode = Mode.Unknown;
    uint? lastInput;
    int busy;                    // 1 = monitörle konuşan bir işlem sürüyor
    Mode pending = Mode.Unknown; // meşgulken basılan kısayol
    SettingsForm openSettings;
    DateTime pollPausedUntil = DateTime.MinValue;

    // Switch moduna geçerken yapılan değişiklikler (geri almak için). Yalnızca arka plan işinde, busy=1 iken değişir.
    bool windowsMoved, audioChanged;
    string savedAudioConsole, savedAudioComm;

    public TrayApp(bool autostart)
    {
        _ = ui.Handle; // UI iş parçacığına geri dönmek için
        settings = AppSettings.Load();
        bool firstRun = !AppSettings.Exists;
        ApplyLook();

        statusItem = new ToolStripMenuItem("Durum: algılanıyor…") { Enabled = false };
        pcItem = new ToolStripMenuItem("Bilgisayar", null, (_, _) => Go(Mode.Pc, true));
        swItem = new ToolStripMenuItem("Nintendo Switch", null, (_, _) => Go(Mode.Switch, true));
        menu.Items.AddRange(new ToolStripItem[]
        {
            statusItem,
            new ToolStripSeparator(),
            pcItem,
            swItem,
            new ToolStripSeparator(),
            soundsItem = new ToolStripMenuItem("Efekt sesleri", null, (_, _) => ToggleSounds()),
            new ToolStripMenuItem("Ayarlar…", null, (_, _) => OpenSettings()),
            new ToolStripMenuItem("Çıkış", null, (_, _) => Exit()),
        });

        tray = new NotifyIcon { Icon = iconUnknown, Text = "EkranGeçiş", ContextMenuStrip = menu, Visible = true };
        tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) ShowTrayMenu(); };

        // Açılış ekranı (Windows açılırken kendiliğinden başlatıldıysa gösterilmez)
        bool splash = !autostart && !firstRun && settings.ShowSplash;
        if (splash)
        {
            new Splash().ShowOn(CursorScreen());
            Sfx.Play(Sound.Acilis);
        }

        hotkeys.Pressed += OnHotkey;
        RegisterHotkeys();
        poll.Tick += (_, _) => Poll();
        poll.Start();
        UpdateUi();

        // Başlangıçta monitörün hangi girişte olduğunu öğren.
        Interlocked.Exchange(ref busy, 1);
        RunBg(() => Ddc.TryReadInput(out uint v) ? v : NoValue, v =>
        {
            Volatile.Write(ref busy, 0);
            if (v == NoValue)
            {
                osd.Flash(CursorScreen(), "DDC/CI ile kontrol edilebilen monitör bulunamadı. Monitör menüsünde DDC/CI açık mı?", Theme.Switch, 6000);
            }
            else
            {
                if (firstRun) { settings.PcInput = v; if (v == settings.SwitchInput) settings.SwitchInput = v == 0x11 ? 0x12u : 0x11u; }
                lastInput = v;
                mode = Classify(v);
            }
            UpdateUi();
            UpdatePanel();

            if (firstRun)
            {
                settings.Save();
                osd.Flash(CursorScreen(), "EkranGeçiş çalışıyor. Simgesi görev çubuğunun sağ köşesinde.", Theme.Pc, 4000);
                OpenSettings();
            }
            else if (!autostart && !splash && v != NoValue)
            {
                osd.Flash(CursorScreen(),
                    "EkranGeçiş çalışıyor.  " + settings.DeviceName + ": " + settings.HotkeySwitch + "   •   Bilgisayar: " + settings.HotkeyPc,
                    Theme.Pc, 3500);
            }
            RunPending();
        });
    }

    // ---------------- Mod geçişi ----------------

    Mode Classify(uint v) =>
        v == settings.PcInput ? Mode.Pc : v == settings.SwitchInput ? Mode.Switch : Mode.Unknown;

    void OnHotkey(int id)
    {
        Log.Write("Kısayol " + id);
        switch (id)
        {
            case HkPc: Go(Mode.Pc, true); break;
            case HkSwitch: Go(Mode.Switch, true); break;
            case HkToggle: Go(mode == Mode.Switch ? Mode.Pc : Mode.Switch, true); break;
            case HkSounds: ToggleSounds(); break;
        }
    }

    /// <summary>Efekt seslerini açıp kapatır (kısayol ya da tepsi menüsünden).</summary>
    void ToggleSounds()
    {
        settings.SoundsEnabled = !settings.SoundsEnabled;
        try { settings.Save(); } catch { }
        ApplyLook(recreate: false);
        UpdateUi();
        if (settings.SoundsEnabled) Sfx.Play(Sound.OtomatikDonus);
        osd.Flash(VisibleScreen(), settings.SoundsEnabled ? "Efekt sesleri açık" : "Efekt sesleri kapalı", Theme.Pc, 1600);
    }

    /// <summary>
    /// Hedef moda geçer. sendDdc=false: monitör zaten değişmiş (ör. monitörün düğmesiyle),
    /// yalnızca pencere/ses ayarlarını uygula.
    /// </summary>
    void Go(Mode target, bool sendDdc)
    {
        if (target == Mode.Unknown) return;
        if (Interlocked.Exchange(ref busy, 1) == 1)
        {
            Log.Write($"Go({target}, ddc={sendDdc}) meşgul -> sıraya alındı");
            if (sendDdc) pending = target;
            return;
        }
        Log.Write($"Go({target}, ddc={sendDdc}) başladı, mevcut mod={mode}");
        pollPausedUntil = DateTime.Now.AddSeconds(10);
        var s = settings;

        RunBg(() =>
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            if (sendDdc)
            {
                bool ddcOk = Ddc.SetInput(target == Mode.Switch ? s.SwitchInput : s.PcInput);
                Log.Write($"  DDC giriş -> {Ddc.InputName(target == Mode.Switch ? s.SwitchInput : s.PcInput)}: {ddcOk} ({sw.ElapsedMilliseconds} ms)");
                if (!ddcOk) return (Ok: false, Warn: "Monitöre komut gönderilemedi. Monitörün menüsünde DDC/CI açık mı?");
            }
            try
            {
                var warn = target == Mode.Switch ? ApplySwitchSide(s) : RevertSwitchSide(s);
                Log.Write($"  bitti ({sw.ElapsedMilliseconds} ms){(warn != null ? " uyarı: " + warn : "")}");
                return (Ok: true, Warn: warn);
            }
            catch (Exception ex)
            {
                Log.Write("  HATA: " + ex);
                return (Ok: true, Warn: "Ekran değişti ama pencere/ses ayarında hata oluştu: " + ex.Message);
            }
        }, r =>
        {
            Volatile.Write(ref busy, 0);
            if (!r.Ok)
            {
                Sfx.Play(Sound.Hata);
                osd.Flash(CursorScreen(), r.Warn, Theme.Switch, 4500);
            }
            else
            {
                if (mode != target || sendDdc)
                    Sfx.Play(target == Mode.Switch ? Sound.KonsolaGecis : sendDdc ? Sound.BilgisayaraDonus : Sound.OtomatikDonus);
                mode = target;
                lastInput = target == Mode.Switch ? s.SwitchInput : s.PcInput;
                UpdateUi();
                UpdatePanel();
                if (r.Warn != null) osd.Flash(VisibleScreen(), r.Warn, Theme.Switch, 5000);
                else if (!sendDdc && target == Mode.Pc)
                    osd.Flash(VisibleScreen(), "Monitör bilgisayara döndü.", Theme.Pc);
            }
            RunPending();
        });
    }

    void RunPending()
    {
        if (pending == Mode.Unknown) return;
        var p = pending;
        pending = Mode.Unknown;
        Go(p, true);
    }

    /// <summary>Arka planda: pencereleri laptop ekranına taşı, sesi değiştir. Uyarı metni döndürür.</summary>
    string ApplySwitchSide(AppSettings s)
    {
        string warn = null;
        if (s.MoveWindows && !windowsMoved)
        {
            var (main, other, _) = Ddc.Layout();
            if (main != IntPtr.Zero && other != IntPtr.Zero)
            {
                int n = WindowMover.MoveAll(main, other);
                Log.Write($"  {n} pencere laptop ekranına taşındı");
                windowsMoved = true;
            }
        }
        if (s.ChangeAudio && !audioChanged && !string.IsNullOrEmpty(s.SwitchAudioId))
        {
            savedAudioConsole = Audio.GetDefaultId(Audio.RoleConsole);
            savedAudioComm = Audio.GetDefaultId(Audio.RoleCommunications);
            bool ok = Audio.SetDefault(s.SwitchAudioId, Audio.RoleConsole)
                    & Audio.SetDefault(s.SwitchAudioId, Audio.RoleMultimedia)
                    & Audio.SetDefault(s.SwitchAudioId, Audio.RoleCommunications);
            Log.Write($"  ses -> {s.SwitchAudioName}: {ok}");
            audioChanged = true;
            if (!ok) warn = "Ses cihazı değiştirilemedi. \"" + s.SwitchAudioName + "\" bağlı mı?";
            else
            {
                // Bazı ses programları (ör. FxSound) kendini hemen tekrar varsayılan yapar; bunu yakala.
                Thread.Sleep(1500);
                string now = Audio.GetDefaultId(Audio.RoleConsole);
                if (now != s.SwitchAudioId)
                {
                    string who = Audio.ListOutputs().FirstOrDefault(d => d.Id == now)?.Name ?? "başka bir program";
                    Log.Write("  ses geri alındı -> " + who);
                    warn = "Ses cihazı değişti ama \"" + who + "\" kendini tekrar varsayılan yaptı. " +
                           "O programın \"varsayılan cihaz olarak kal\" ayarını kapatın.";
                }
            }
        }
        return warn;
    }

    /// <summary>Arka planda: Switch modunda yapılan değişiklikleri geri al.</summary>
    string RevertSwitchSide(AppSettings s)
    {
        if (windowsMoved)
        {
            var (_, other, _) = Ddc.Layout();
            int n = WindowMover.RestoreAll(other);
            Log.Write($"  {n} pencere eski yerine kondu");
            windowsMoved = false;
        }
        if (audioChanged)
        {
            if (s.RestoreAudio && savedAudioConsole != null)
            {
                bool ok = Audio.SetDefault(savedAudioConsole, Audio.RoleConsole)
                        & Audio.SetDefault(savedAudioConsole, Audio.RoleMultimedia);
                if (savedAudioComm != null) Audio.SetDefault(savedAudioComm, Audio.RoleCommunications);
                Log.Write($"  ses eski cihaza döndü: {ok}");
            }
            audioChanged = false;
        }
        return null;
    }

    /// <summary>
    /// Birkaç saniyede bir monitörün girişini okur. Monitör kendiliğinden (Switch kapanınca)
    /// veya düğmesiyle değiştirilirse durumu ve pencere/ses ayarlarını buna uydurur.
    /// </summary>
    void Poll()
    {
        if (openSettings != null || DateTime.Now < pollPausedUntil) return;
        if (Interlocked.Exchange(ref busy, 1) == 1) return;
        RunBg(() => Ddc.TryReadInput(out uint v) ? v : NoValue, v =>
        {
            Volatile.Write(ref busy, 0);
            if (pending != Mode.Unknown) { RunPending(); return; }
            if (v == NoValue) return;
            lastInput = v;
            var m = Classify(v);
            if (m != mode && m != Mode.Unknown) Log.Write($"İzleme: monitör girişi {Ddc.InputName(v)}, uygulama modu {mode}");
            if (m == Mode.Pc && mode == Mode.Switch) Go(Mode.Pc, false);
            else if (m == Mode.Switch && mode == Mode.Pc) Go(Mode.Switch, false);
            else if (mode == Mode.Unknown && m != Mode.Unknown) { mode = m; UpdateUi(); UpdatePanel(); }
            else UpdateUi();
        });
    }

    // ---------------- Arayüz ----------------

    void UpdateUi()
    {
        string name = mode switch { Mode.Pc => "Bilgisayar", Mode.Switch => settings.DeviceName, _ => "bilinmiyor" };
        string input = lastInput.HasValue ? " (" + Ddc.InputName(lastInput.Value) + ")" : "";
        statusItem.Text = "Şu an: " + name + input;
        tray.Text = Trim63("EkranGeçiş – " + name + input);
        tray.Icon = mode switch { Mode.Pc => iconPc, Mode.Switch => iconSw, _ => iconUnknown };

        pcItem.Text = "Bilgisayar (" + Ddc.InputName(settings.PcInput) + ")";
        swItem.Text = settings.DeviceName + " (" + Ddc.InputName(settings.SwitchInput) + ")";
        pcItem.Checked = mode == Mode.Pc;
        swItem.Checked = mode == Mode.Switch;
        pcItem.ShortcutKeyDisplayString = settings.HotkeyPc.IsEmpty ? null : settings.HotkeyPc.ToString();
        swItem.ShortcutKeyDisplayString = settings.HotkeySwitch.IsEmpty ? null : settings.HotkeySwitch.ToString();
        soundsItem.Checked = settings.SoundsEnabled;
        soundsItem.ShortcutKeyDisplayString = settings.HotkeySounds.IsEmpty ? null : settings.HotkeySounds.ToString();
    }

    void UpdatePanel()
    {
        if (mode == Mode.Switch && settings.ShowMiniPanel)
        {
            var hk = !settings.HotkeyPc.IsEmpty ? settings.HotkeyPc : settings.HotkeyToggle;
            panel.ShowOn(LaptopScreen(), hk.ToString(), settings.DeviceName);
        }
        else if (panel.Visible) panel.Hide();
    }

    static string Trim63(string s) => s.Length <= 63 ? s : s.Substring(0, 63);

    static Screen LaptopScreen()
    {
        var (_, _, dev) = Ddc.Layout();
        return Screen.AllScreens.FirstOrDefault(sc => sc.DeviceName == dev) ?? Screen.PrimaryScreen;
    }

    static Screen CursorScreen() => Screen.FromPoint(Cursor.Position);

    Screen VisibleScreen() => mode == Mode.Switch ? LaptopScreen() : CursorScreen();

    void ShowTrayMenu()
    {
        try
        {
            typeof(NotifyIcon).GetMethod("ShowContextMenu", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(tray, null);
        }
        catch { }
    }

    void RegisterHotkeys()
    {
        hotkeys.UnregisterAll();
        var failed = new List<string>();
        if (!hotkeys.Register(HkPc, settings.HotkeyPc)) failed.Add(settings.HotkeyPc.ToString());
        if (!hotkeys.Register(HkSwitch, settings.HotkeySwitch)) failed.Add(settings.HotkeySwitch.ToString());
        if (!hotkeys.Register(HkToggle, settings.HotkeyToggle)) failed.Add(settings.HotkeyToggle.ToString());
        if (!hotkeys.Register(HkSounds, settings.HotkeySounds)) failed.Add(settings.HotkeySounds.ToString());
        if (failed.Count > 0)
            osd.Flash(CursorScreen(), "Bu kısayollar başka bir program tarafından kullanılıyor: " +
                      string.Join(", ", failed) + ". Ayarlardan değiştirebilirsiniz.", Theme.Switch, 6000);
    }

    void OpenSettings()
    {
        if (openSettings != null)
        {
            openSettings.Activate();
            return;
        }
        hotkeys.UnregisterAll(); // kutuya kısayol girerken mevcutlar tetiklenmesin
        using (var f = new SettingsForm(settings))
        {
            openSettings = f;
            if (f.ShowDialog() == DialogResult.OK && f.Result != null)
            {
                bool themeChanged = f.Result.Theme != settings.Theme;
                settings = f.Result;
                settings.Save();
                if (themeChanged)
                {
                    panel.Close();
                    osd.Close();
                }
                ApplyLook(recreate: themeChanged);
            }
            openSettings = null;
        }
        pollPausedUntil = DateTime.Now.AddSeconds(6);
        RegisterHotkeys();
        if (lastInput.HasValue && mode == Mode.Unknown) mode = Classify(lastInput.Value);
        UpdateUi();
        UpdatePanel();
    }

    /// <summary>Temayı, simgeleri, ses ayarlarını uygular; tema değiştiyse panelleri yeniden oluşturur.</summary>
    void ApplyLook(bool recreate = true)
    {
        Sfx.Enabled = settings.SoundsEnabled;
        Sfx.Volume = settings.SoundVolume;
        if (!recreate) return;
        Pal.Apply(settings.Theme);
        iconPc = IconFactory.Make(Theme.Pc);
        iconSw = IconFactory.Make(Theme.Switch);
        iconUnknown = IconFactory.Make(Theme.Unknown);
        menu.BackColor = Pal.Surface;
        menu.ForeColor = Pal.Text;
        panel = new MiniPanel { ContextMenuStrip = menu };
        panel.BackToPc += () => Go(Mode.Pc, true);
        osd = new Osd();
    }

    void Exit()
    {
        poll.Stop();
        if (windowsMoved || audioChanged)
        {
            try { RevertSwitchSide(settings); } catch { }
        }
        hotkeys.Dispose();
        tray.Visible = false;
        tray.Dispose();
        panel.Close();
        osd.Close();
        ExitThread();
    }

    /// <summary>İşi arka planda çalıştırır, sonucu UI iş parçacığında işler.</summary>
    void RunBg<T>(Func<T> work, Action<T> done)
    {
        Task.Run(work).ContinueWith(t =>
        {
            T result = t.IsFaulted ? default : t.Result;
            ui.BeginInvoke(() => done(result));
        });
    }
}


