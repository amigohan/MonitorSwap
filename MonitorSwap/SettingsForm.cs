using System.Diagnostics;

namespace MonitorSwap;

/// <summary>Ayarlar penceresi: solda gezinme, sağda sayfalar, altta Kaydet / İptal.</summary>
sealed class SettingsForm : Form
{
    public AppSettings Result { get; private set; }

    readonly AppSettings s;
    readonly Sidebar sidebar = new();
    readonly List<Page> pages = new();
    readonly Footer footer = new();

    // Kontroller
    List<uint> inputCodes = new();
    readonly Segmented segPc = new("DisplayPort") { SelectedColor = Pal.Pc };
    readonly ToggleSwitch tglAdventure = new(), tglSplash = new(), tglSfx = new();
    readonly HotkeyField hkSounds = new();
    readonly VolumeEditor volume = new();
    readonly Segmented segSw = new("HDMI-1") { SelectedColor = Pal.Switch };
    readonly TextField txtName = new();
    readonly PillButton btnTest = new("Dene", ButtonKind.Secondary, Glyph.Play);
    readonly SettingRow rowTest;
    readonly MonitorStatus status = new();
    readonly ToggleSwitch tglAudio = new(), tglRestore = new(), tglMove = new(), tglPanel = new(), tglAuto = new();
    readonly Picker picker = new();
    readonly HotkeyField hkPc = new(), hkSw = new(), hkToggle = new();
    readonly PillButton btnLog = new("Aç", ButtonKind.Secondary, Glyph.Folder);

    public SettingsForm(AppSettings current)
    {
        s = current.Clone();
        Text = "Monitor Swap – Ayarlar";
        Icon = IconFactory.App();
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = true;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Pal.Bg;
        ForeColor = Pal.Text;
        Font = new Font(Fonts.Text, 10f);
        KeyPreview = true;

        LoadValues();
        rowTest = new SettingRow("Girişi dene", "Monitörü 5 saniyeliğine konsol girişine alır, sonra geri döner.", btnTest);
        BuildPages();

        Controls.Add(footer);
        Controls.Add(sidebar);
        foreach (var p in pages) Controls.Add(p);
        sidebar.SetPages(pages);
        sidebar.PageSelected += ShowPage;
        ShowPage(0);

        footer.Save.Click += (_, _) => Save();
        footer.Cancel.DialogResult = DialogResult.Cancel;
        AcceptButton = footer.Save;
        CancelButton = footer.Cancel;

        btnTest.Click += async (_, _) => await TestSwitchInput();
        btnLog.Click += (_, _) => OpenLog();
        tglAudio.CheckedChanged += (_, _) => UpdateEnabled();
        tglSfx.CheckedChanged += (_, _) => UpdateEnabled();
        volume.Test.Click += (_, _) =>
        {
            bool e = Sfx.Enabled; int v = Sfx.Volume;
            Sfx.Enabled = true; Sfx.Volume = volume.Slider.Value;
            Sfx.Play(Sound.KonsolaGecis);
            Sfx.Enabled = e; Sfx.Volume = v;
        };
        UpdateEnabled();

        Shown += async (_, _) => await DetectMonitor();
        ApplySize();
    }

    // ---------------- Sayfalar ----------------

    void BuildPages()
    {
        var monitor = new Page("Monitör", "Hangi girişin bilgisayara, hangisinin konsola ait olduğunu seç.", Glyph.Monitor);
        monitor.Add(status);
        monitor.Add(new Card(
            new SettingRow("Bilgisayar girişi", "Bilgisayarın monitöre bağlı olduğu giriş.", segPc),
            new SettingRow("Konsol girişi", "Konsolun (ör. Nintendo Switch dock'u) takılı olduğu giriş.", segSw),
            new SettingRow("Cihaz adı", "Menüde ve panelde görünecek ad (ör. Nintendo Switch, PS5).", txtName),
            rowTest));

        var audio = new Page("Ses", "Konsola geçerken bilgisayarın sesinin nereye gideceğini seç.", Glyph.Volume);
        audio.Add(new Card(
            new SettingRow("Ses çıkışını değiştir", "Konsol modunda bilgisayar sesini başka bir cihaza aktar.", tglAudio),
            new SettingRow("Konsol modunda ses cihazı", null, picker),
            new SettingRow("Dönüşte eski cihaza geç", "Bilgisayara dönünce önceki ses cihazı geri gelir.", tglRestore)));
        audio.Add(new Callout(CalloutKind.Info,
            "Konsolun kendi sesi HDMI üzerinden monitöre gider; monitörün hoparlöründen veya kulaklık girişinden duyulur. " +
            "Bu ayar yalnızca bilgisayarın sesini (Discord, müzik vb.) yönlendirir."));
        if (picker.Options.Any(o => o.Text.Contains("FxSound", StringComparison.OrdinalIgnoreCase)))
            audio.Add(new Callout(CalloutKind.Warning,
                "FxSound kendini sürekli varsayılan ses cihazı yapıyor ve yapılan değişikliği hemen geri alıyor. " +
                "Bu özelliği kullanmak için FxSound'u kapatın ya da ayarlarından bu davranışı devre dışı bırakın."));

        var windows = new Page("Pencereler", "Konsol modundayken masaüstünün nasıl davranacağını seç.", Glyph.Windows);
        windows.Add(new Card(
            new SettingRow("Pencereleri diğer ekrana taşı",
                "Ana monitördeki pencereler ikinci ekrana (ör. laptop ekranı) alınır, bilgisayara dönünce eski yerlerine konur.", tglMove),
            new SettingRow("Kontrol paneli",
                "Konsol modunda ikinci ekranın köşesinde \"Bilgisayara dön\" paneli gösterilir.", tglPanel)));

        var keys = new Page("Kısayollar", "Kutuya tıkla ve yeni tuş kombinasyonuna bas. Silmek için Backspace.", Glyph.Keyboard);
        keys.Add(new Card(
            new SettingRow("Konsola geç", null, hkSw),
            new SettingRow("Bilgisayara dön", null, hkPc),
            new SettingRow("İkisi arasında geçiş", null, hkToggle),
            new SettingRow("Efekt seslerini aç/kapat", null, hkSounds)));
        keys.Add(new Callout(CalloutKind.Info,
            "Türkçe klavyede Ctrl+Alt, AltGr ile aynıdır. Kısayola Shift eklemezsen #, $, { gibi karakterleri yazamayabilirsin."));

        var general = new Page("Genel", "Görünüm, sesler ve uygulamanın kendisiyle ilgili ayarlar.", Glyph.Settings);
        general.Add(new Card(
            new SettingRow("Macera teması", "Yeşil-altın renkler, süslü çerçeveler ve altın başlıklar. Kaydedince tüm uygulamaya uygulanır.", tglAdventure),
            new SettingRow("Açılış ekranı", "Uygulama başlarken logoyu kısa süre göster.", tglSplash),
            new SettingRow("Efekt sesleri", "Geçişlerde kısa okarina sesleri çalsın.", tglSfx),
            new SettingRow("Ses seviyesi", null, volume)));
        general.Add(new Card(
            new SettingRow("Windows ile başlat", "Oturum açınca simge görev çubuğuna kendiliğinden gelir.", tglAuto),
            new SettingRow("Kayıt dosyası", "Bir şey ters giderse ne olduğunu buradan görebilirsin.", btnLog),
            new SettingRow("Sürüm", "Monitor Swap") { Value = Application.ProductVersion }));

        pages.AddRange(new[] { monitor, audio, windows, keys, general });
    }

    public void ShowPage(int index)
    {
        for (int i = 0; i < pages.Count; i++) pages[i].Visible = i == index;
        sidebar.Selected = index;
        pages[index].Relayout();
    }

    // ---------------- Yerleşim ----------------

    int S(float v) => (int)Math.Round(v * DeviceDpi / 96f);

    void ApplySize() => ClientSize = new Size(S(880), S(720));

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        ApplySize();
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        int sw = S(232), fh = S(76);
        sidebar.SetBounds(0, 0, sw, ClientSize.Height);
        footer.SetBounds(sw, ClientSize.Height - fh, ClientSize.Width - sw, fh);
        foreach (var p in pages) p.SetBounds(sw, 0, ClientSize.Width - sw, ClientSize.Height - fh);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Dwm.DarkTitle(Handle, Pal.Sidebar);
    }

    // ---------------- Değerler ----------------

    /// <summary>Giriş seçeneklerini doldurur; seçili girişler listede yoksa sona eklenir.</summary>
    void SetInputs(IEnumerable<uint> codes, uint pc, uint sw)
    {
        inputCodes = codes.Concat(new[] { pc, sw }).Distinct().ToList();
        var names = inputCodes.Select(Ddc.InputName).ToArray();
        segPc.SetItems(names, inputCodes.IndexOf(pc));
        segSw.SetItems(names, inputCodes.IndexOf(sw));
    }

    uint SelectedPc => inputCodes[segPc.SelectedIndex];
    uint SelectedSw => inputCodes[segSw.SelectedIndex];

    void LoadValues()
    {
        SetInputs(Ddc.DefaultInputs, s.PcInput, s.SwitchInput);
        txtName.Text = s.DeviceName;

        List<Audio.Device> devices;
        try { devices = Audio.ListOutputs(); } catch { devices = new(); }
        foreach (var d in devices) picker.Options.Add((d.Id, d.Name));
        if (!string.IsNullOrEmpty(s.SwitchAudioId) && devices.All(d => d.Id != s.SwitchAudioId))
            picker.Options.Add((s.SwitchAudioId, s.SwitchAudioName + " (bağlı değil)"));
        picker.Select(!string.IsNullOrEmpty(s.SwitchAudioId) ? s.SwitchAudioId
            : devices.FirstOrDefault(d => !d.Name.Contains("FxSound", StringComparison.OrdinalIgnoreCase))?.Id);

        tglAudio.Checked = s.ChangeAudio;
        tglRestore.Checked = s.RestoreAudio;
        tglMove.Checked = s.MoveWindows;
        tglPanel.Checked = s.ShowMiniPanel;
        try { tglAuto.Checked = AppSettings.Autostart; } catch { }
        hkPc.Value = s.HotkeyPc;
        hkSw.Value = s.HotkeySwitch;
        hkToggle.Value = s.HotkeyToggle;
        tglAdventure.Checked = s.Theme == "Macera";
        hkSounds.Value = s.HotkeySounds;
        tglSplash.Checked = s.ShowSplash;
        tglSfx.Checked = s.SoundsEnabled;
        volume.Slider.Value = s.SoundVolume;
    }

    void UpdateEnabled()
    {
        picker.Enabled = tglAudio.Checked;
        tglRestore.Enabled = tglAudio.Checked;
        volume.Enabled = tglSfx.Checked;
    }

    async Task DetectMonitor()
    {
        var (model, inputs, input) = await Task.Run(() =>
        {
            try
            {
                var caps = Ddc.ReadCapabilities();
                uint? v = Ddc.TryReadInput(out uint x) ? x : null;
                return (caps.Model, caps.Inputs, v);
            }
            catch { return ((string)null, (List<uint>)null, (uint?)null); }
        });
        if (IsDisposed || Disposing) return; // pencere algılama bitmeden kapandıysa
        if (inputs != null) SetInputs(inputs, SelectedPc, SelectedSw);
        status.SetInfo(model, input);
        sidebar.SetStatus(model);
        pages[0].Relayout();
    }

    async Task TestSwitchInput()
    {
        uint sw = SelectedSw, pc = SelectedPc;
        if (sw == pc)
        {
            rowTest.DescriptionColor = Pal.Warning;
            rowTest.Description = "Bilgisayar ve konsol girişi aynı olamaz.";
            return;
        }
        btnTest.Enabled = false;
        rowTest.DescriptionColor = Pal.TextDim;
        rowTest.Description = "Monitör 5 saniyeliğine " + Ddc.InputName(sw) + " girişine geçiyor…";
        bool ok = await Task.Run(() =>
        {
            if (!Ddc.SetInput(sw)) return false;
            Thread.Sleep(5000);
            return Ddc.SetInput(pc);
        });
        if (IsDisposed || Disposing) return;
        rowTest.DescriptionColor = ok ? Pal.Success : Pal.Warning;
        rowTest.Description = ok
            ? "Tamam. Konsolun görüntüsünü gördüysen doğru giriş seçili."
            : "Monitöre komut gönderilemedi. Monitör menüsünde DDC/CI açık mı?";
        btnTest.Enabled = true;
    }

    void OpenLog()
    {
        try
        {
            if (File.Exists(Log.FilePath)) Process.Start(new ProcessStartInfo(Log.FilePath) { UseShellExecute = true });
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Log.FilePath)!);
                Process.Start(new ProcessStartInfo(Path.GetDirectoryName(Log.FilePath)!) { UseShellExecute = true });
            }
        }
        catch { }
    }

    void Save()
    {
        uint pc = SelectedPc, sw = SelectedSw;
        if (pc == sw) { footer.ShowError("Bilgisayar ve konsol için farklı girişler seç."); ShowPage(0); return; }

        var keys = new[] { hkPc.Value, hkSw.Value, hkToggle.Value, hkSounds.Value }.Where(h => !h.IsEmpty).Select(h => h.ToString()).ToList();
        if (keys.Count != keys.Distinct().Count()) { footer.ShowError("Aynı kısayol birden fazla işe atanmış."); ShowPage(3); return; }

        if (tglAudio.Checked && picker.SelectedId == null) { footer.ShowError("Konsol modu için bir ses cihazı seç."); ShowPage(1); return; }

        s.PcInput = pc;
        s.SwitchInput = sw;
        s.DeviceName = string.IsNullOrWhiteSpace(txtName.Text) ? "Konsol" : txtName.Text.Trim();
        s.ChangeAudio = tglAudio.Checked;
        if (picker.SelectedId != null)
        {
            s.SwitchAudioId = picker.SelectedId;
            s.SwitchAudioName = picker.SelectedText.Replace(" (bağlı değil)", "");
        }
        s.RestoreAudio = tglRestore.Checked;
        s.MoveWindows = tglMove.Checked;
        s.ShowMiniPanel = tglPanel.Checked;
        s.HotkeyPc = hkPc.Value;
        s.HotkeySwitch = hkSw.Value;
        s.HotkeyToggle = hkToggle.Value;
        s.Theme = tglAdventure.Checked ? "Macera" : "Koyu";
        s.HotkeySounds = hkSounds.Value;
        s.ShowSplash = tglSplash.Checked;
        s.SoundsEnabled = tglSfx.Checked;
        s.SoundVolume = volume.Slider.Value;
        try { AppSettings.Autostart = tglAuto.Checked; } catch { }

        Result = s;
        DialogResult = DialogResult.OK;
        Close();
    }
}

// =====================================================================
//  Ayarlar penceresine özel parçalar
// =====================================================================

/// <summary>Sol gezinme çubuğu: logo, monitör durumu, sayfa listesi.</summary>
sealed class Sidebar : DControl
{
    readonly List<NavItem> items = new();
    string model;
    bool detecting = true;
    public event Action<int> PageSelected;

    public Sidebar() { BackColor = Pal.Sidebar; }

    public void SetPages(IList<Page> pages)
    {
        for (int i = 0; i < pages.Count; i++)
        {
            int idx = i;
            var n = new NavItem(pages[i].Title, pages[i].IconGlyph);
            n.Click += (_, _) => PageSelected?.Invoke(idx);
            items.Add(n);
            Controls.Add(n);
        }
        PerformLayout();
    }

    public int Selected
    {
        set { for (int i = 0; i < items.Count; i++) items[i].Selected = i == value; }
    }

    public void SetStatus(string model)
    {
        this.model = model;
        detecting = false;
        Invalidate();
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        int y = S(118);
        foreach (var n in items)
        {
            n.SetBounds(S(10), y, Width - S(20), S(42));
            y += S(46);
        }
    }

    protected override void Render(Graphics g)
    {
        // Logo
        IconFactory.DrawEmblem(g, new RectangleF(S(14), S(18), S(52), S(52)));
        Draw.Text(g, "Monitor Swap", Fonts.Subtitle(DeviceDpi), new Rectangle(S(72), S(22), Width - S(80), S(26)), Pal.Adventure ? Pal.Gold : Pal.Text);

        // Durum
        Color dot = detecting ? Pal.Neutral : model != null ? Pal.Success : Pal.Warning;
        string text = detecting ? "Monitör aranıyor…" : model != null ? model + " bağlı" : "Monitör bulunamadı";
        g.FillEllipse(new SolidBrush(dot), S(72), S(52), S(8), S(8));
        Draw.Text(g, text, Fonts.Caption(DeviceDpi), new Rectangle(S(86), S(44), Width - S(92), S(22)), Pal.TextDim);

        using var pen = new Pen(Pal.Border);
        g.DrawLine(pen, Width - 1, 0, Width - 1, Height);
        g.DrawLine(pen, S(20), S(92), Width - S(20), S(92));
    }
}

sealed class NavItem : DControl
{
    readonly string glyph;
    bool selected;

    public NavItem(string text, string glyph)
    {
        Text = text;
        this.glyph = glyph;
        BackColor = Pal.Sidebar;
        Cursor = Cursors.Hand;
    }

    public bool Selected
    {
        get => selected;
        set { selected = value; Invalidate(); }
    }

    protected override void Render(Graphics g)
    {
        var r = new RectangleF(0, 0, Width, Height);
        if (selected) Draw.Fill(g, Pal.SurfaceHi, r, Sf(8));
        else if (hover) Draw.Fill(g, Pal.Mix(Pal.Sidebar, Pal.Hover, 0.7f), r, Sf(8));
        if (selected) Draw.Fill(g, Pal.Accent, new RectangleF(0, Height * 0.28f, Sf(3), Height * 0.44f), Sf(1.5f));

        Color c = selected ? Pal.Text : hover ? Pal.Text : Pal.TextDim;
        Draw.Icon(g, glyph, 11.5f, DeviceDpi, new Rectangle(S(12), 0, S(24), Height), selected ? Pal.Lighten(Pal.Accent, 0.25f) : c);
        Draw.Text(g, Text, selected ? Fonts.BodySemi(DeviceDpi) : Fonts.Body(DeviceDpi),
            new Rectangle(S(46), 0, Width - S(50), Height), c);
    }
}

/// <summary>Monitör sayfasının üstündeki durum kartı.</summary>
sealed class MonitorStatus : DControl, IBlock
{
    string model;
    uint? input;
    bool loaded;

    public MonitorStatus() { BackColor = Pal.Bg; }

    public void SetInfo(string model, uint? input)
    {
        this.model = model;
        this.input = input;
        loaded = true;
        Invalidate();
    }

    public int LayoutFor(int width)
    {
        Width = width;
        Height = S(88);
        return Height;
    }

    protected override void Render(Graphics g)
    {
        var r = new RectangleF(0, 0, Width, Height);
        Draw.Fill(g, Pal.Surface, r, Sf(10));
        Draw.Stroke(g, Pal.Border, r, Sf(10));

        bool ok = model != null;
        Color tint = !loaded ? Pal.Neutral : ok ? Pal.Accent : Pal.Warning;
        var icon = new RectangleF(S(18), (Height - S(48)) / 2f, S(48), S(48));
        Draw.Fill(g, Pal.Mix(Pal.Surface, tint, 0.16f), icon, Sf(12));
        Draw.Icon(g, Glyph.Monitor, 17f, DeviceDpi, Rectangle.Round(icon), Pal.Lighten(tint, 0.2f));

        string title = !loaded ? "Monitör aranıyor…" : ok ? model : "DDC/CI monitör bulunamadı";
        string sub = !loaded ? "Biraz bekle" : ok ? "DDC/CI bağlantısı çalışıyor" : "Monitörün menüsünde DDC/CI açık olmalı";
        int x = S(82);
        Draw.Text(g, title, Fonts.Subtitle(DeviceDpi), new Rectangle(x, S(20), Width - x - S(170), S(26)), Pal.Text);
        if (loaded) g.FillEllipse(new SolidBrush(ok ? Pal.Success : Pal.Warning), x, S(56), S(8), S(8));
        Draw.Text(g, sub, Fonts.Caption(DeviceDpi), new Rectangle(x + (loaded ? S(14) : 0), S(48), Width - x - S(170), S(22)), Pal.TextDim);

        if (input.HasValue)
        {
            var font = Fonts.CaptionSemi(DeviceDpi);
            string label = "Şu an: " + Ddc.InputName(input.Value);
            int w = Draw.Measure(label, font).Width + S(24);
            var pill = new RectangleF(Width - w - S(18), (Height - S(28)) / 2f, w, S(28));
            Draw.Fill(g, Pal.SurfaceHi, pill, S(14));
            Draw.Stroke(g, Pal.BorderHi, pill, S(14));
            Draw.Text(g, label, font, Rectangle.Round(pill), Pal.Text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }
    }
}

/// <summary>Alt çubuk: hata mesajı + İptal / Kaydet.</summary>
sealed class Footer : DControl
{
    public readonly PillButton Save = new("Kaydet", ButtonKind.Primary, Glyph.Check) { MinWidth = 120 };
    public readonly PillButton Cancel = new("İptal", ButtonKind.Secondary) { MinWidth = 100 };
    string error;

    public Footer()
    {
        BackColor = Pal.Bg;
        Save.BackColor = Pal.Bg;
        Cancel.BackColor = Pal.Bg;
        Controls.Add(Save);
        Controls.Add(Cancel);
    }

    public void ShowError(string message)
    {
        error = message;
        Invalidate();
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        var ps = Save.Preferred;
        var pc = Cancel.Preferred;
        int y = (Height - ps.Height) / 2;
        Save.SetBounds(Width - S(32) - ps.Width, y, ps.Width, ps.Height);
        Cancel.SetBounds(Save.Left - S(10) - pc.Width, y, pc.Width, pc.Height);
    }

    protected override void Render(Graphics g)
    {
        using var pen = new Pen(Pal.Border);
        g.DrawLine(pen, 0, 0, Width, 0);
        if (error != null)
        {
            Draw.Icon(g, Glyph.Warning, 11f, DeviceDpi, new Rectangle(S(30), 0, S(20), Height), Pal.Warning);
            Draw.Text(g, error, Fonts.Body(DeviceDpi), new Rectangle(S(58), 0, Cancel.Left - S(70), Height), Pal.Warning);
        }
    }
}

