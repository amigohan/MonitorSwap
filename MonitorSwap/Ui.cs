using System.Drawing.Drawing2D;
using static MonitorSwap.Native;

namespace MonitorSwap;

static class Theme
{
    public static Color Pc => Pal.Pc;
    public static Color Switch => Pal.Switch;
    public static Color Unknown => Pal.Neutral;
}

/// <summary>
/// Uygulamanın logosu: mavi kalkan, arkasından çapraz geçen kılıç (kabzası sağ üstte, ucu sol altta)
/// ve kalkanın üzerinde yeşil bir okarina. 160×160 birimlik bir tuvalde tanımlıdır, istenen boyuta ölçeklenir.
/// Tepsi simgesinde sağ alttaki küçük nokta modu gösterir.
/// </summary>
static class IconFactory
{
    // Ayar aracında seçilen değerler
    const float SwordAngle = 31, GripLength = 25, BladeShow = 11;
    const float OcarinaAngle = 124, OcarinaScale = 0.35f;
    const bool OcarinaMirrored = true;

    static Graphics Prepare(Bitmap bmp)
    {
        var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.Clear(Color.Transparent);
        return g;
    }

    static SolidBrush B(int rgb, float alpha = 1f) => new(Color.FromArgb((int)(alpha * 255), Pal.C(rgb)));

    static Pen P(int rgb, float width, float alpha = 1f) => new(Color.FromArgb((int)(alpha * 255), Pal.C(rgb)), width)
    {
        StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round,
    };

    static PointF Pt(float x, float y) => new(x, y);

    /// <summary>Quadratic Bézier'i GDI+'ın kübik Bézier'ine çevirerek ekler.</summary>
    static void Quad(GraphicsPath p, PointF a, PointF c, PointF b) =>
        p.AddBezier(a, Pt(a.X + 2f / 3 * (c.X - a.X), a.Y + 2f / 3 * (c.Y - a.Y)),
                       Pt(b.X + 2f / 3 * (c.X - b.X), b.Y + 2f / 3 * (c.Y - b.Y)), b);

    static void Fill(Graphics g, Brush b, GraphicsPath p, float dx = 0, float dy = 0)
    {
        if (dx == 0 && dy == 0) { g.FillPath(b, p); return; }
        var st = g.Save(); g.TranslateTransform(dx, dy); g.FillPath(b, p); g.Restore(st);
    }

    // ---------------- Kalkan ----------------

    static GraphicsPath ShieldPath()
    {
        var p = new GraphicsPath();
        p.AddLine(80, 30, 128, 41);
        p.AddLine(128, 41, 128, 80);
        p.AddBezier(128, 80, 128, 109, 107, 128, 80, 140);
        p.AddBezier(80, 140, 53, 128, 32, 109, 32, 80);
        p.AddLine(32, 80, 32, 41);
        p.CloseFigure();
        return p;
    }

    static void Scaled(Graphics g, float s, Action draw)
    {
        var st = g.Save();
        g.TranslateTransform(80, 85); g.ScaleTransform(s, s); g.TranslateTransform(-80, -85);
        draw();
        g.Restore(st);
    }

    static void DrawShield(Graphics g)
    {
        using var sh = ShieldPath();
        using (var b = B(0x0D0F13, 0.45f)) Fill(g, b, sh, 2, 4);
        using (var b = B(0x7C8391)) g.FillPath(b, sh);
        using (var b = B(0xD9DEE7)) Scaled(g, 0.97f, () => g.FillPath(b, sh));
        using (var b = B(0xF3F5F9, 0.55f))
            g.FillPolygon(b, new[] { Pt(80, 31), Pt(128, 42), Pt(128, 60), Pt(80, 50), Pt(32, 60), Pt(32, 42) });
        using (var b = B(0x1D4A8A)) Scaled(g, 0.85f, () => g.FillPath(b, sh));
        using (var b = B(0x2F6FBF)) Scaled(g, 0.82f, () => g.FillPath(b, sh));
        using (var facet = new GraphicsPath())
        {
            facet.AddLine(80, 42, 118, 51);
            facet.AddLine(118, 51, 118, 80);
            facet.AddBezier(118, 80, 118, 84, 117, 88, 116, 91);
            facet.AddLine(116, 91, 80, 82);
            facet.CloseFigure();
            using var b = B(0x3C81D4, 0.75f);
            g.FillPath(b, facet);
        }
        using (var pen = P(0x9CC2F0, 1.4f, 0.7f)) Scaled(g, 0.72f, () => g.DrawPath(pen, sh));
        using var rivet = B(0xF4F6FA);
        using var rivetRim = P(0x7C8391, 1f);
        foreach (var (x, y) in new[] { (80, 35), (52, 40), (108, 40), (37, 62), (123, 62), (40, 96), (120, 96), (58, 124), (102, 124), (80, 135) })
        {
            g.FillEllipse(rivet, x - 2.4f, y - 2.4f, 4.8f, 4.8f);
            g.DrawEllipse(rivetRim, x - 2.4f, y - 2.4f, 4.8f, 4.8f);
        }
    }

    // ---------------- Kılıç ----------------

    static void DrawSword(Graphics g)
    {
        float r = SwordAngle * (float)Math.PI / 180;
        float cx = 84 + (float)Math.Sin(r) * BladeShow, cy = 82 - (float)Math.Cos(r) * BladeShow;
        float tip = 100 + BladeShow, gy = -63 - GripLength, py = gy - 7;
        var st = g.Save();
        g.TranslateTransform(cx, cy);
        g.RotateTransform(SwordAngle);

        var blade = new[] { Pt(-6.5f, -52), Pt(6.5f, -52), Pt(6.5f, tip - 32), Pt(0, tip), Pt(-6.5f, tip - 32) };
        using (var b = B(0x4A505C)) { var s2 = g.Save(); g.TranslateTransform(1.5f, 1.5f); g.FillPolygon(b, blade); g.Restore(s2); }
        using (var b = B(0xEEF1F6)) g.FillPolygon(b, new[] { Pt(-6.5f, -52), Pt(0, -52), Pt(0, tip), Pt(-6.5f, tip - 32) });
        using (var b = B(0xA9B2C1)) g.FillPolygon(b, new[] { Pt(0, -52), Pt(6.5f, -52), Pt(6.5f, tip - 32), Pt(0, tip) });
        using (var p = P(0x7F8899, 1.6f)) g.DrawLine(p, 0, -48, 0, tip - 40);

        // Balçak
        using (var guard = new GraphicsPath())
        {
            Quad(guard, Pt(-27, -61), Pt(0, -58), Pt(27, -61));
            Quad(guard, Pt(27, -61), Pt(29, -56), Pt(26, -52));
            guard.AddLine(26, -52, -26, -52);
            Quad(guard, Pt(-26, -52), Pt(-29, -56), Pt(-27, -61));
            guard.CloseFigure();
            using var b = B(0xD6B257);
            using var p = P(0x8A6A24, 1.2f);
            g.FillPath(b, guard);
            g.DrawPath(p, guard);
        }
        using (var plate = Draw.Round(new RectangleF(-7, -63, 14, 11), 2.5f))
        using (var b = B(0xC49A3E))
        using (var p = P(0x8A6A24, 1.2f)) { g.FillPath(b, plate); g.DrawPath(p, plate); }

        // Mavi kabza ve sargıları
        using (var grip = Draw.Round(new RectangleF(-5.5f, gy, 11, GripLength), 3))
        using (var b = B(0x2B66B3)) g.FillPath(b, grip);
        using (var p = new Pen(Pal.C(0x1A447C), 2f))
            for (float y = gy + 6; y < -63; y += 6) g.DrawLine(p, -5.5f, y, 5.5f, y - 4);

        // Topuz
        using (var b = B(0x8A6A24)) g.FillEllipse(b, -8.5f, py - 8.5f, 17, 17);
        using (var b = B(0xD6B257)) g.FillEllipse(b, -7, py - 7, 14, 14);
        using (var b = B(0xFFF3CF)) g.FillEllipse(b, -4.2f, py - 4.2f, 4.4f, 4.4f);
        g.Restore(st);
    }

    // ---------------- Okarina ----------------

    static void DrawOcarina(Graphics g)
    {
        var st = g.Save();
        g.TranslateTransform(80, 84);
        if (OcarinaMirrored) g.ScaleTransform(-1, 1);
        g.RotateTransform(OcarinaAngle);
        g.ScaleTransform(OcarinaScale, OcarinaScale);
        g.TranslateTransform(-82, -86);

        using var mouth = new GraphicsPath();
        mouth.AddLine(34, 96, 54, 110);
        mouth.AddLine(54, 110, 40, 140);
        Quad(mouth, Pt(40, 140), Pt(34, 148), Pt(26, 142));
        mouth.AddLine(26, 142, 18, 136);
        Quad(mouth, Pt(18, 136), Pt(12, 130), Pt(18, 124));
        mouth.CloseFigure();

        using var body = new GraphicsPath();
        body.AddBezier(14, 52, 14, 30, 44, 24, 76, 34);
        body.AddBezier(76, 34, 108, 44, 136, 74, 150, 112);
        body.AddBezier(150, 112, 154, 124, 146, 132, 134, 128);
        body.AddBezier(134, 128, 110, 120, 88, 116, 66, 114);
        body.AddBezier(66, 114, 38, 112, 14, 82, 14, 52);
        body.CloseFigure();

        using (var b = B(0x1D5C37)) Fill(g, b, mouth, 2, 4);
        using (var b = B(0x26794A)) g.FillPath(b, mouth);
        using (var b = B(0x1D5C37)) Fill(g, b, body, 2, 5);
        using (var b = B(0x3FAE6A)) g.FillPath(b, body);
        using (var p = P(0xB8F2CF, 7, 0.75f)) g.DrawBeziers(p, new[] { Pt(24, 46), Pt(30, 34), Pt(52, 32), Pt(72, 38), Pt(96, 46), Pt(118, 64), Pt(132, 88) });
        using (var p = P(0x26794A, 6, 0.6f)) g.DrawBezier(p, 30, 92, 50, 106, 80, 108, 108, 112);

        using var rim = B(0xB8F2CF, 0.55f);
        using var hole = B(0x0C3320);
        foreach (var (x, y, rr) in new[] { (40f, 48f, 7f), (60f, 50f, 8f), (82f, 58f, 7.5f), (102f, 70f, 7f), (58f, 66f, 4.2f),
                                           (54f, 92f, 8f), (94f, 98f, 4f), (92f, 108f, 5.5f), (112f, 104f, 6.5f), (130f, 114f, 5.5f) })
        {
            float r1 = rr + 1.6f;
            g.FillEllipse(rim, x - r1, y - r1, r1 * 2, r1 * 2);
            g.FillEllipse(hole, x - rr, y - rr, rr * 2, rr * 2);
        }
        g.Restore(st);
    }

    /// <summary>
    /// Logonun gerçek sınırlarını (kılıcın topuzu ve ucu, balçak, kalkan ve gölgesi) hesaplar,
    /// hiçbir parça taşmadan 160×160 tuvali dolduracak ölçek ve kaydırmayı döndürür.
    /// </summary>
    static readonly (float S, float Tx, float Ty) Fit = ComputeFit();

    static (float, float, float) ComputeFit()
    {
        var pts = new List<PointF> { Pt(32, 30), Pt(132, 30), Pt(32, 144), Pt(132, 144) }; // kalkan + gölgesi
        double r = SwordAngle * Math.PI / 180, cos = Math.Cos(r), sin = Math.Sin(r);
        float cx = 84 + (float)sin * BladeShow, cy = 82 - (float)cos * BladeShow;
        float tip = 100 + BladeShow, py = -63 - GripLength - 7;
        PointF W(float x, float y) => Pt(cx + (float)(x * cos - y * sin), cy + (float)(x * sin + y * cos));
        pts.Add(W(0, tip + 2));
        pts.AddRange(new[] { W(-29, -61), W(29, -61), W(-29, -52), W(29, -52) });
        var pc = W(0, py);
        pts.AddRange(new[] { Pt(pc.X - 9, pc.Y - 9), Pt(pc.X + 9, pc.Y + 9) });

        float minX = pts.Min(p => p.X), maxX = pts.Max(p => p.X), minY = pts.Min(p => p.Y), maxY = pts.Max(p => p.Y);
        const float margin = 3;
        float s = (160 - 2 * margin) / Math.Max(maxX - minX, maxY - minY);
        return (s, 80 - s * (minX + maxX) / 2, 80 - s * (minY + maxY) / 2);
    }

    /// <summary>Logoyu verilen kareye çizer. <paramref name="dot"/> verilirse sağ alta mod noktası ekler.</summary>
    public static void DrawEmblem(Graphics g, RectangleF area, Color? dot = null)
    {
        var state = g.Save();
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TranslateTransform(area.X, area.Y);
        float k = area.Width / 160f;
        g.ScaleTransform(k, k);

        var inner = g.Save();
        g.TranslateTransform(Fit.Tx, Fit.Ty); g.ScaleTransform(Fit.S, Fit.S);
        DrawSword(g);
        DrawShield(g);
        DrawOcarina(g);
        g.Restore(inner);

        if (dot.HasValue)
        {
            using var edge = B(0x101114);
            using var fill = new SolidBrush(dot.Value);
            g.FillEllipse(edge, 112, 112, 46, 46);
            g.FillEllipse(fill, 119, 119, 32, 32);
        }
        g.Restore(state);
    }

    static Icon ToIcon(Bitmap bmp)
    {
        IntPtr h = bmp.GetHicon();
        var icon = (Icon)Icon.FromHandle(h).Clone();
        DestroyIcon(h);
        return icon;
    }

    /// <summary>Tepsi simgesi; sağ alttaki nokta modu gösterir.</summary>
    public static Icon Make(Color mode)
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Prepare(bmp)) DrawEmblem(g, new RectangleF(0, 0, 32, 32), mode);
        return ToIcon(bmp);
    }

    /// <summary>Pencere ikonu.</summary>
    public static Icon App()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Prepare(bmp)) DrawEmblem(g, new RectangleF(0, 0, 32, 32));
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
            using (var g = Prepare(bmp)) DrawEmblem(g, new RectangleF(0, 0, sz, sz));
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
        Dwm.Rounded(Handle, small: false, border: Pal.Adventure ? Pal.GoldDim : Pal.BorderHi);
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

static class Ornament
{
    /// <summary>Macera temasında pencere köşelerine ince altın köşebentler çizer.</summary>
    public static void Corners(Graphics g, Rectangle r, int inset, int len, float width)
    {
        if (!Pal.Adventure) return;
        using var pen = new Pen(Pal.Gold, width);
        int l = r.Left + inset, t = r.Top + inset, ri = r.Right - inset - 1, b = r.Bottom - inset - 1;
        g.DrawLines(pen, new[] { new Point(l, t + len), new Point(l, t), new Point(l + len, t) });
        g.DrawLines(pen, new[] { new Point(ri - len, t), new Point(ri, t), new Point(ri, t + len) });
        g.DrawLines(pen, new[] { new Point(l, b - len), new Point(l, b), new Point(l + len, b) });
        g.DrawLines(pen, new[] { new Point(ri - len, b), new Point(ri, b), new Point(ri, b - len) });
    }

    /// <summary>Ortasında küçük bir baklava olan ince ayraç çizgisi.</summary>
    public static void Divider(Graphics g, int x, int y, int w, float s)
    {
        using var pen = new Pen(Pal.Adventure ? Pal.GoldDim : Pal.Border, 1);
        int cx = x + w / 2;
        g.DrawLine(pen, x, y, cx - 8 * s, y);
        g.DrawLine(pen, cx + 8 * s, y, x + w, y);
        using var b = new SolidBrush(Pal.Adventure ? Pal.Gold : Pal.BorderHi);
        float d = 4 * s;
        g.FillPolygon(b, new[] { new PointF(cx, y - d), new PointF(cx + d, y), new PointF(cx, y + d), new PointF(cx - d, y) });
    }
}

/// <summary>Konsol modundayken ikinci ekranda duran küçük kontrol paneli; köşesinde nefes alan bir rehber ışık var.</summary>
sealed class MiniPanel : FloatingForm
{
    public event Action BackToPc;
    readonly PillButton btn = new("Bilgisayara dön", ButtonKind.Primary, Glyph.Back);
    readonly IconButton close = new(Glyph.Close) { IconSize = 8f };
    readonly System.Windows.Forms.Timer glow = new() { Interval = 33 };
    string[] keys = Array.Empty<string>();
    string deviceName = "Nintendo Switch";
    string hotkeyText = "";
    double phase;
    bool overOrb;

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
        glow.Tick += (_, _) => { phase += 0.033; Invalidate(OrbRect); };
        VisibleChanged += (_, _) => { if (Visible) glow.Start(); else glow.Stop(); };
    }

    protected override Size DesiredSize() => new(S(320), S(170));

    Rectangle OrbRect => new(Width - S(76), S(16), S(30), S(30));

    public void ShowOn(Screen screen, string hotkeyText, string deviceName = "Nintendo Switch")
    {
        this.deviceName = deviceName;
        this.hotkeyText = hotkeyText;
        keys = hotkeyText.Split('+');
        if (!Visible) Show();
        AnchorTo(screen, center: false);
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        bool over = OrbRect.Contains(e.Location);
        if (over != overOrb) { overOrb = over; Invalidate(); }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (overOrb) { overOrb = false; Invalidate(); }
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        close.SetBounds(Width - S(42), S(16), S(26), S(26));
        btn.SetBounds(S(20), S(74), Width - S(40), S(40));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Pal.Surface);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        Ornament.Corners(g, ClientRectangle, S(6), S(12), Sf(1.4f));

        // Amblem (konsol modunda kırmızı halkalı)
        IconFactory.DrawEmblem(g, new RectangleF(S(14), S(10), S(48), S(48)));

        Draw.Text(g, deviceName, Pal.Adventure ? Fonts.Display(11.5f, DeviceDpi) : Fonts.BodySemi(DeviceDpi),
            new Rectangle(S(68), S(14), Width - S(152), S(24)), Pal.Text);
        string sub = overOrb ? "Dönmek için " + hotkeyText : "Ana monitörde şu an konsol var";
        Draw.Text(g, sub, Fonts.Caption(DeviceDpi), new Rectangle(S(68), S(38), Width - S(110), S(18)), overOrb ? Pal.Gold : Pal.TextDim);

        DrawOrb(g);

        // Kısayol
        int cy = S(144);
        Draw.Text(g, "Kısayol", Fonts.Caption(DeviceDpi), new Rectangle(S(20), cy - S(10), S(60), S(20)), Pal.TextMuted);
        if (keys.Length > 0) Draw.KeyChips(g, keys, S(76), cy, DeviceDpi);
    }

    /// <summary>Rehber ışık: yavaşça büyüyüp küçülen, iç içe yumuşak halkalardan oluşan bir ışık.</summary>
    void DrawOrb(Graphics g)
    {
        var r = OrbRect;
        float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f;
        double breath = (Math.Sin(phase * 2 * Math.PI / 2.4) + 1) / 2; // 2,4 sn'lik nefes
        float core = Sf(4.5f + (float)breath * 1.2f);
        Color light = Pal.Adventure ? Pal.C(0xCFF7E6) : Pal.C(0xD6E6FF);
        for (int i = 5; i >= 1; i--)
        {
            float rad = core + Sf(i * 2.2f) * (0.85f + (float)breath * 0.3f);
            int a = (int)(18 + breath * 14) * (6 - i) / 5;
            using var b = new SolidBrush(Color.FromArgb(Math.Clamp(a, 0, 255), light));
            g.FillEllipse(b, cx - rad, cy - rad, rad * 2, rad * 2);
        }
        using (var b = new SolidBrush(light)) g.FillEllipse(b, cx - core, cy - core, core * 2, core * 2);
        using (var w = new SolidBrush(Color.White)) g.FillEllipse(w, cx - core * 0.45f, cy - core * 0.45f, core * 0.9f, core * 0.9f);
    }
}

/// <summary>Uygulama açılırken kısa süre görünen logo ekranı. Tıklayınca hemen kapanır.</summary>
sealed class Splash : FloatingForm
{
    readonly System.Windows.Forms.Timer timer = new() { Interval = 16 };
    readonly DateTime start = DateTime.Now;
    Screen screen;
    bool closing;
    DateTime closeStart;

    public Splash()
    {
        Opacity = 0;
        Click += (_, _) => BeginClose();
        timer.Tick += (_, _) => Step();
    }

    protected override Size DesiredSize() => new(S(380), S(250));

    public void ShowOn(Screen s)
    {
        screen = s;
        Show();
        ApplySize();
        Center();
        timer.Start();
    }

    void Center()
    {
        if (screen == null) return;
        var wa = screen.WorkingArea;
        Location = new Point(wa.Left + (wa.Width - Width) / 2, wa.Top + (wa.Height - Height) / 2);
    }

    protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); Center(); }

    void BeginClose()
    {
        if (closing) return;
        closing = true;
        closeStart = DateTime.Now;
    }

    void Step()
    {
        double t = (DateTime.Now - start).TotalSeconds;
        if (!closing)
        {
            Opacity = Math.Min(1, t / 0.35);
            if (t > 2.1) BeginClose();
        }
        else
        {
            double c = (DateTime.Now - closeStart).TotalSeconds;
            Opacity = Math.Max(0, 1 - c / 0.45);
            if (c >= 0.45) { timer.Stop(); Close(); Dispose(); }
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Pal.Surface);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        Ornament.Corners(g, ClientRectangle, S(8), S(18), Sf(1.6f));

        float size = S(104);
        IconFactory.DrawEmblem(g, new RectangleF((Width - size) / 2f, S(18), size, size));

        var ft = Pal.Adventure ? Fonts.Display(22f, DeviceDpi) : Fonts.Title(DeviceDpi);
        Draw.Text(g, "Monitor Swap", ft, new Rectangle(0, S(138), Width, S(40)), Pal.Adventure ? Pal.Gold : Pal.Text,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        Ornament.Divider(g, (Width - S(180)) / 2, S(188), S(180), DeviceDpi / 96f);
        Draw.Text(g, "Bilgisayar ile konsol arasında tek tuşla geçiş", Fonts.Caption(DeviceDpi), new Rectangle(0, S(200), Width, S(24)), Pal.TextDim,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
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



