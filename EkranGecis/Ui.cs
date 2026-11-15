using System.Drawing.Drawing2D;
using static EkranGecis.Native;

namespace EkranGecis;

static class Theme
{
    public static Color Pc => Pal.Accent;
    public static Color Switch => Pal.Switch;
    public static Color Unknown => Pal.Neutral;
}

/// <summary>Tepsi simgesi: renkli ekranlı küçük bir monitör.</summary>
static class IconFactory
{
    static Graphics Prepare(Bitmap bmp, float size)
    {
        var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.Clear(Color.Transparent);
        g.ScaleTransform(size / 32f, size / 32f);
        return g;
    }

    /// <summary>Tepsi simgesi (32 birimlik tuvalde).</summary>
    static void DrawTray(Graphics g, Color screen)
    {
        Draw.Fill(g, Pal.C(0xE6E8EE), new RectangleF(1, 3, 30, 21), 5);  // çerçeve
        Draw.Fill(g, screen, new RectangleF(3.5f, 5.5f, 25, 16), 3);      // ekran
        Draw.Fill(g, Pal.C(0xE6E8EE), new RectangleF(13.5f, 24, 5, 3), 0);
        Draw.Fill(g, Pal.C(0xE6E8EE), new RectangleF(8, 26.5f, 16, 4), 2);
    }

    /// <summary>Uygulama ikonu: koyu kare üzerinde, ekranı mavi/kırmızı ikiye bölünmüş monitör.</summary>
    static void DrawApp(Graphics g)
    {
        Draw.Fill(g, Pal.C(0x1E2026), new RectangleF(0.5f, 0.5f, 31, 31), 7.5f);
        Draw.Stroke(g, Pal.C(0x3B3F4A), new RectangleF(0.5f, 0.5f, 31, 31), 7.5f, 0.75f);
        var frame = new RectangleF(5, 7.5f, 22, 15);
        Draw.Fill(g, Pal.C(0xE6E8EE), frame, 3);
        var scr = new RectangleF(6.75f, 9.25f, 18.5f, 11.5f);
        using (var clip = Draw.Round(scr, 1.75f))
        {
            g.SetClip(clip);
            using var blue = new SolidBrush(Pal.Accent);
            using var red = new SolidBrush(Pal.Switch);
            g.FillRectangle(blue, scr);
            g.FillPolygon(red, new[] { new PointF(scr.Right + 1, scr.Top - 1), new PointF(scr.Right + 1, scr.Bottom + 1), new PointF(scr.Left + 5, scr.Bottom + 1) });
            g.ResetClip();
        }
        Draw.Fill(g, Pal.C(0xE6E8EE), new RectangleF(14.5f, 22.5f, 3, 2.5f), 0);
        Draw.Fill(g, Pal.C(0xE6E8EE), new RectangleF(10.5f, 24.5f, 11, 2.5f), 1.25f);
    }

    static Icon ToIcon(Bitmap bmp)
    {
        IntPtr h = bmp.GetHicon();
        var icon = (Icon)Icon.FromHandle(h).Clone();
        DestroyIcon(h);
        return icon;
    }

    public static Icon Make(Color screen)
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Prepare(bmp, 32)) DrawTray(g, screen);
        return ToIcon(bmp);
    }

    public static Icon App()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Prepare(bmp, 32)) DrawApp(g);
        return ToIcon(bmp);
    }

    /// <summary>Uygulama ikonunu çok boyutlu .ico dosyası olarak yazar (exe ikonu için).</summary>
    public static void SaveAppIco(string path)
    {
        int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 128, 256 };
        var images = new List<byte[]>();
        foreach (int sz in sizes)
        {
            using var bmp = new Bitmap(sz, sz);
            using (var g = Prepare(bmp, sz)) DrawApp(g);
            using var ms = new MemoryStream();
            bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            images.Add(ms.ToArray());
        }
        using var w = new BinaryWriter(File.Create(path));
        w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
        int offset = 6 + 16 * sizes.Length;
        for (int i = 0; i < sizes.Length; i++)
        {
            byte d = (byte)(sizes[i] >= 256 ? 0 : sizes[i]);
            w.Write(d); w.Write(d); w.Write((byte)0); w.Write((byte)0);
            w.Write((short)1); w.Write((short)32);
            w.Write(images[i].Length); w.Write(offset);
            offset += images[i].Length;
        }
        foreach (var img in images) w.Write(img);
    }
}

/// <summary>Odak çalmayan, her zaman üstte, yuvarlak köşeli koyu küçük pencere.</summary>
abstract class FloatingForm : Form
{
    Screen anchorScreen;
    bool anchorCenter;

    protected FloatingForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Pal.Surface;
        ForeColor = Pal.Text;
        DoubleBuffered = true;
        SetStyle(ControlStyles.ResizeRedraw, true);
    }

    protected int S(float v) => (int)Math.Round(v * DeviceDpi / 96f);
    protected float Sf(float v) => v * DeviceDpi / 96f;

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= (int)(WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW) | 0x8 /*WS_EX_TOPMOST*/;
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Dwm.Rounded(Handle, small: false, border: Pal.BorderHi);
    }

    /// <summary>Bu DPI'da istenen boyut.</summary>
    protected abstract Size DesiredSize();

    protected void ApplySize()
    {
        var sz = DesiredSize();
        if (ClientSize != sz) ClientSize = sz;
    }

    protected void MakeDraggable(Control c)
    {
        c.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Left) return;
            anchorScreen = null; // kullanıcı taşıdıysa artık otomatik yerleştirme yapma
            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
        };
    }

    /// <summary>Ekranın sağ altına (veya alt ortasına) yerleştirir; DPI/boyut değişince yeniden hizalar.</summary>
    protected void AnchorTo(Screen s, bool center)
    {
        anchorScreen = s;
        anchorCenter = center;
        ApplySize();
        Reposition();
    }

    void Reposition()
    {
        if (anchorScreen == null) return;
        var wa = anchorScreen.WorkingArea;
        int m = S(18);
        Location = anchorCenter
            ? new Point(wa.Left + (wa.Width - Width) / 2, wa.Bottom - Height - S(72))
            : new Point(wa.Right - Width - m, wa.Bottom - Height - m);
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        Reposition();
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        if (IsHandleCreated) BeginInvoke(() => { ApplySize(); Reposition(); PerformLayout(); Invalidate(true); });
    }
}

/// <summary>Switch modundayken laptop ekranında duran küçük kontrol paneli.</summary>
sealed class MiniPanel : FloatingForm
{
    public event Action BackToPc;
    readonly PillButton btn = new("Bilgisayara dön", ButtonKind.Primary, Glyph.Back);
    readonly IconButton close = new(Glyph.Close) { IconSize = 8f };
    string[] keys = Array.Empty<string>();
    string deviceName = "Nintendo Switch";

    public MiniPanel()
    {
        btn.BackColor = Pal.Surface;
        close.BackColor = Pal.Surface;
        btn.TabStop = false;
        btn.Click += (_, _) => BackToPc?.Invoke();
        close.Click += (_, _) => Hide();
        Controls.Add(btn);
        Controls.Add(close);
        MakeDraggable(this);
    }

    protected override Size DesiredSize() => new(S(316), S(166));

    public void ShowOn(Screen screen, string hotkeyText, string deviceName = "Nintendo Switch")
    {
        this.deviceName = deviceName;
        keys = hotkeyText.Split('+');
        if (!Visible) Show();
        AnchorTo(screen, center: false);
        Invalidate();
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        close.SetBounds(Width - S(40), S(14), S(26), S(26));
        btn.SetBounds(S(18), S(70), Width - S(36), S(40));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Pal.Surface);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // Switch rozeti
        var badge = new RectangleF(S(18), S(16), S(38), S(38));
        Draw.Fill(g, Pal.Mix(Pal.Surface, Pal.Switch, 0.18f), badge, Sf(10));
        Draw.Icon(g, Glyph.Game, 14f, DeviceDpi, Rectangle.Round(badge), Pal.Lighten(Pal.Switch, 0.2f));

        Draw.Text(g, deviceName + " ekranda", Fonts.BodySemi(DeviceDpi), new Rectangle(S(66), S(16), Width - S(112), S(20)), Pal.Text);
        Draw.Text(g, "Ana monitör konsolu gösteriyor", Fonts.Caption(DeviceDpi), new Rectangle(S(66), S(36), Width - S(112), S(18)), Pal.TextDim);

        // Kısayol
        int cy = S(140);
        Draw.Text(g, "Kısayol", Fonts.Caption(DeviceDpi), new Rectangle(S(18), cy - S(10), S(60), S(20)), Pal.TextMuted);
        if (keys.Length > 0) Draw.KeyChips(g, keys, S(74), cy, DeviceDpi);
    }
}

/// <summary>Kısa süreli bilgi/uyarı balonu.</summary>
sealed class Osd : FloatingForm
{
    readonly System.Windows.Forms.Timer timer = new() { Interval = 2600 };
    string text = "";
    Color color = Pal.Accent;

    public Osd()
    {
        timer.Tick += (_, _) => { timer.Stop(); Hide(); };
        Click += (_, _) => Hide();
    }

    int TextWidth => S(380);

    protected override Size DesiredSize()
    {
        int h = Draw.WrappedHeight(text, Fonts.Body(DeviceDpi), TextWidth);
        return new Size(TextWidth + S(74), Math.Max(S(58), h + S(32)));
    }

    public void Flash(Screen screen, string message, Color c, int ms = 2600)
    {
        text = message;
        color = c;
        if (!Visible) Show();
        AnchorTo(screen, center: true);
        Invalidate();
        timer.Stop();
        timer.Interval = ms;
        timer.Start();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Pal.Surface);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        bool warn = color.ToArgb() == Pal.Switch.ToArgb() || color.ToArgb() == Pal.Warning.ToArgb();
        Color tint = warn ? Pal.Warning : color;
        var badge = new RectangleF(S(16), (Height - S(30)) / 2f, S(30), S(30));
        Draw.Fill(g, Pal.Mix(Pal.Surface, tint, 0.18f), badge, S(15));
        Draw.Icon(g, warn ? Glyph.Warning : Glyph.Info, 11f, DeviceDpi, Rectangle.Round(badge), Pal.Lighten(tint, 0.15f));
        int h = Draw.WrappedHeight(text, Fonts.Body(DeviceDpi), TextWidth);
        Draw.Wrapped(g, text, Fonts.Body(DeviceDpi), new Rectangle(S(58), (Height - h) / 2, TextWidth, h + 2), Pal.Text);
    }
}

