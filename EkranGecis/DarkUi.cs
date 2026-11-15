using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace EkranGecis;

// =====================================================================
//  Tasarım belirteçleri: renkler, yazı tipleri, ikonlar
// =====================================================================

/// <summary>Koyu tema renk paleti. Metin renkleri koyu yüzeylerde en az 7:1 kontrast verir.</summary>
static class Pal
{
    public static readonly Color Bg = C(0x131418);
    public static readonly Color Sidebar = C(0x17181D);
    public static readonly Color Surface = C(0x1E2026);
    public static readonly Color SurfaceHi = C(0x272A32);
    public static readonly Color Hover = C(0x30333C);
    public static readonly Color Pressed = C(0x383C46);
    public static readonly Color Border = C(0x2B2E36);
    public static readonly Color BorderHi = C(0x3B3F4A);
    public static readonly Color Text = C(0xECEEF3);
    public static readonly Color TextDim = C(0xA9AEBB);
    public static readonly Color TextMuted = C(0x7A7F8C);
    public static readonly Color Accent = C(0x5B8DEF);
    public static readonly Color Switch = C(0xEF5464);
    public static readonly Color Success = C(0x45C98C);
    public static readonly Color Warning = C(0xE9B44C);
    public static readonly Color Neutral = C(0x8A8F9C);

    public static Color C(int rgb) => Color.FromArgb(255, (rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255);

    public static Color Mix(Color a, Color b, float t) => Color.FromArgb(
        (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));

    public static Color Lighten(Color c, float t) => Mix(c, Color.White, t);
    public static Color Darken(Color c, float t) => Mix(c, Color.Black, t);
}

/// <summary>Windows 11 yazı tipleri (yoksa klasik Segoe UI). Boyutlar DPI'a göre piksel olarak üretilir.</summary>
static class Fonts
{
    static readonly HashSet<string> Installed = LoadInstalled();
    static HashSet<string> LoadInstalled()
    {
        using var col = new InstalledFontCollection();
        return new HashSet<string>(col.Families.Select(f => f.Name));
    }
    static string Pick(params string[] names) => names.FirstOrDefault(Installed.Contains) ?? "Segoe UI";

    public static readonly string Text = Pick("Segoe UI Variable Text", "Segoe UI");
    public static readonly string TextSemi = Pick("Segoe UI Variable Text Semibold", "Segoe UI Semibold");
    public static readonly string Display = Pick("Segoe UI Variable Display Semib", "Segoe UI Semibold");
    public static readonly string Icons = Pick("Segoe Fluent Icons", "Segoe MDL2 Assets");

    static readonly Dictionary<(string, float, int), Font> Cache = new();

    public static Font Get(string family, float pt, int dpi)
    {
        var key = (family, pt, dpi);
        if (!Cache.TryGetValue(key, out var f))
            Cache[key] = f = new Font(family, pt * dpi / 72f, FontStyle.Regular, GraphicsUnit.Pixel);
        return f;
    }

    public static Font Body(int dpi) => Get(Text, 10.5f, dpi);
    public static Font BodySemi(int dpi) => Get(TextSemi, 10.5f, dpi);
    public static Font Caption(int dpi) => Get(Text, 9.25f, dpi);
    public static Font CaptionSemi(int dpi) => Get(TextSemi, 9f, dpi);
    public static Font Subtitle(int dpi) => Get(TextSemi, 13f, dpi);
    public static Font Title(int dpi) => Get(Display, 20f, dpi);
    public static Font Icon(float pt, int dpi) => Get(Icons, pt, dpi);
}

/// <summary>Segoe Fluent Icons karakterleri.</summary>
static class Glyph
{
    public const string Monitor = "", Volume = "", Windows = "", Keyboard = "",
        Settings = "", Game = "", Pc = "", Close = "", ChevronDown = "",
        Check = "", Info = "", Warning = "", Play = "", Folder = "",
        Back = "";
}

static class Draw
{
    public static GraphicsPath Round(RectangleF r, float rad)
    {
        var p = new GraphicsPath();
        float d = Math.Min(rad * 2, Math.Min(r.Width, r.Height));
        if (d <= 0.5f) { p.AddRectangle(r); return p; }
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public static void Fill(Graphics g, Color c, RectangleF r, float rad)
    {
        using var b = new SolidBrush(c);
        using var p = Round(r, rad);
        g.FillPath(b, p);
    }

    public static void Stroke(Graphics g, Color c, RectangleF r, float rad, float width = 1f)
    {
        var rr = RectangleF.Inflate(r, -width / 2f, -width / 2f);
        using var pen = new Pen(c, width);
        using var p = Round(rr, rad);
        g.DrawPath(pen, p);
    }

    const TextFormatFlags Base = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;

    public static void Text(Graphics g, string s, Font f, Rectangle r, Color c,
        TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis)
        => TextRenderer.DrawText(g, s, f, r, c, flags | Base);

    public static void Wrapped(Graphics g, string s, Font f, Rectangle r, Color c)
        => TextRenderer.DrawText(g, s, f, r, c, TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | Base);

    public static Size Measure(string s, Font f) => TextRenderer.MeasureText(s ?? "", f, Size.Empty, Base | TextFormatFlags.SingleLine);

    public static int WrappedHeight(string s, Font f, int width)
    {
        if (string.IsNullOrEmpty(s)) return 0;
        return TextRenderer.MeasureText(s, f, new Size(Math.Max(1, width), int.MaxValue),
            Base | TextFormatFlags.WordBreak | TextFormatFlags.Left | TextFormatFlags.Top).Height;
    }

    public static void Icon(Graphics g, string glyph, float pt, int dpi, Rectangle r, Color c)
        => TextRenderer.DrawText(g, glyph, Fonts.Icon(pt, dpi), r, c,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | Base);

    /// <summary>Klavye tuşu görünümünde küçük etiketler çizer; kapladığı genişliği döndürür.</summary>
    public static int KeyChips(Graphics g, IEnumerable<string> keys, int x, int centerY, int dpi, bool measureOnly = false)
    {
        float s = dpi / 96f;
        var font = Fonts.CaptionSemi(dpi);
        int h = (int)(24 * s), padX = (int)(8 * s), gap = (int)(5 * s);
        int start = x;
        foreach (var k in keys)
        {
            int w = Math.Max(Measure(k, font).Width + padX * 2, h);
            if (!measureOnly)
            {
                var r = new Rectangle(x, centerY - h / 2, w, h);
                Fill(g, Pal.C(0x24262D), r, 5 * s);                                          // alt kenar (tuş derinliği)
                Fill(g, Pal.C(0x383B46), new RectangleF(r.X, r.Y, r.Width, r.Height - 2 * s), 5 * s); // tuş yüzü
                Stroke(g, Pal.C(0x4A4E5A), new RectangleF(r.X, r.Y, r.Width, r.Height - 2 * s), 5 * s);
                Text(g, k, font, new Rectangle(r.X, r.Y, r.Width, r.Height - (int)(1 * s)), Pal.Text,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }
            x += w + gap;
        }
        return x > start ? x - start - gap : 0;
    }
}

/// <summary>Windows 11 pencere çerçevesi ayarları (koyu başlık çubuğu, yuvarlak köşe).</summary>
static class Dwm
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    static int Bgr(Color c) => c.R | (c.G << 8) | (c.B << 16);

    public static void DarkTitle(IntPtr hwnd, Color caption)
    {
        int one = 1, cap = Bgr(caption), text = Bgr(Pal.Text), border = Bgr(Pal.Border);
        DwmSetWindowAttribute(hwnd, 20, ref one, 4);     // DWMWA_USE_IMMERSIVE_DARK_MODE
        DwmSetWindowAttribute(hwnd, 35, ref cap, 4);     // DWMWA_CAPTION_COLOR
        DwmSetWindowAttribute(hwnd, 36, ref text, 4);    // DWMWA_TEXT_COLOR
        DwmSetWindowAttribute(hwnd, 34, ref border, 4);  // DWMWA_BORDER_COLOR
    }

    public static void Rounded(IntPtr hwnd, bool small = false, Color? border = null)
    {
        int pref = small ? 3 : 2;
        DwmSetWindowAttribute(hwnd, 33, ref pref, 4);    // DWMWA_WINDOW_CORNER_PREFERENCE
        if (border.HasValue)
        {
            int b = Bgr(border.Value);
            DwmSetWindowAttribute(hwnd, 34, ref b, 4);
        }
    }
}

// =====================================================================
//  Temel kontrol
// =====================================================================

interface IPreferred { Size Preferred { get; } }

abstract class DControl : Control
{
    protected bool hover, pressed;

    protected DControl()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Pal.Surface;
        ForeColor = Pal.Text;
    }

    protected int S(float v) => (int)Math.Round(v * DeviceDpi / 96f);
    protected float Sf(float v) => v * DeviceDpi / 96f;

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = false; pressed = false; Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left) { pressed = true; Invalidate(); } }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); pressed = false; Invalidate(); }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        if (BackColor.A == 255) g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        Render(g);
    }

    protected abstract void Render(Graphics g);

    /// <summary>Devre dışıyken renkleri arka plana doğru soldurur.</summary>
    protected Color En(Color c) => Enabled ? c : Pal.Mix(c, BackColor.A == 255 ? BackColor : Pal.Surface, 0.55f);

    protected void FocusRing(Graphics g, RectangleF r, float rad)
    {
        if (Focused && ShowFocusCues)
            Draw.Stroke(g, Pal.Lighten(Pal.Accent, 0.25f), RectangleF.Inflate(r, -1, -1), rad, Sf(2));
    }
}

// =====================================================================
//  Kontroller
// =====================================================================

/// <summary>Aç/kapa anahtarı (yumuşak animasyonlu).</summary>
sealed class ToggleSwitch : DControl, IPreferred
{
    bool on;
    float t; // 0 = kapalı, 1 = açık
    readonly System.Windows.Forms.Timer anim = new() { Interval = 12 };

    public event EventHandler CheckedChanged;

    public bool Checked
    {
        get => on;
        set
        {
            if (on == value) return;
            on = value;
            if (IsHandleCreated && Visible) anim.Start(); else { t = on ? 1 : 0; Invalidate(); }
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public ToggleSwitch()
    {
        SetStyle(ControlStyles.Selectable, true);
        TabStop = true;
        Cursor = Cursors.Hand;
        anim.Tick += (_, _) =>
        {
            float target = on ? 1 : 0;
            t += (target - t) * 0.35f;
            if (Math.Abs(target - t) < 0.02f) { t = target; anim.Stop(); }
            Invalidate();
        };
    }

    public Size Preferred => new(S(44), S(24));

    protected override void OnClick(EventArgs e) { base.OnClick(e); if (Enabled) { Focus(); Checked = !Checked; } }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Space && Enabled) Checked = !Checked;
    }

    protected override void Render(Graphics g)
    {
        var r = new RectangleF(0, 0, Width, Height);
        float rad = Height / 2f;
        Color offTrack = hover ? Pal.Hover : Pal.SurfaceHi;
        Color onTrack = hover ? Pal.Lighten(Pal.Accent, 0.08f) : Pal.Accent;
        Draw.Fill(g, En(Pal.Mix(offTrack, onTrack, t)), r, rad);
        if (t < 0.99f) Draw.Stroke(g, En(Pal.Mix(Pal.BorderHi, onTrack, t)), r, rad);

        float d = Height - Sf(8) + Sf(2) * t;
        float x0 = Sf(4), x1 = Width - Sf(4) - d;
        float x = x0 + (x1 - x0) * t;
        var knob = new RectangleF(x, (Height - d) / 2f, d, d);
        Draw.Fill(g, En(Pal.Mix(Pal.TextDim, Color.White, t)), knob, d / 2f);
        FocusRing(g, r, rad);
    }
}

/// <summary>Yan yana seçenekler (ör. DisplayPort | HDMI-1 | HDMI-2).</summary>
sealed class Segmented : DControl, IPreferred
{
    string[] items;
    int selected, hot = -1;
    public Color SelectedColor { get; set; } = Pal.Accent;
    public event EventHandler SelectedChanged;

    /// <summary>Seçenekleri değiştirir (seçili sıra korunmaz, olay tetiklenmez).</summary>
    public void SetItems(string[] newItems, int selectedIndex)
    {
        items = newItems;
        selected = Math.Clamp(selectedIndex, 0, items.Length - 1);
        Invalidate();
    }

    public Segmented(params string[] items)
    {
        this.items = items;
        SetStyle(ControlStyles.Selectable, true);
        TabStop = true;
        Cursor = Cursors.Hand;
    }

    public int SelectedIndex
    {
        get => selected;
        set
        {
            value = Math.Clamp(value, 0, items.Length - 1);
            if (selected == value) return;
            selected = value;
            Invalidate();
            SelectedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    int SegWidth => items.Max(i => Draw.Measure(i, Fonts.BodySemi(DeviceDpi)).Width) + S(26);
    public Size Preferred => new(SegWidth * items.Length + S(8), S(36));

    int IndexAt(Point p) => Math.Clamp((p.X - S(4)) / Math.Max(1, (Width - S(8)) / items.Length), 0, items.Length - 1);

    protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); int i = IndexAt(e.Location); if (i != hot) { hot = i; Invalidate(); } }
    protected override void OnMouseLeave(EventArgs e) { hot = -1; base.OnMouseLeave(e); }
    protected override void OnMouseClick(MouseEventArgs e) { base.OnMouseClick(e); if (Enabled) { Focus(); SelectedIndex = IndexAt(e.Location); } }
    protected override bool IsInputKey(Keys k) => k is Keys.Left or Keys.Right || base.IsInputKey(k);
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Left) SelectedIndex--;
        if (e.KeyCode == Keys.Right) SelectedIndex++;
    }

    protected override void Render(Graphics g)
    {
        var r = new RectangleF(0, 0, Width, Height);
        Draw.Fill(g, Pal.SurfaceHi, r, Sf(9));
        Draw.Stroke(g, Pal.Border, r, Sf(9));
        float segW = (Width - Sf(8)) / items.Length;
        for (int i = 0; i < items.Length; i++)
        {
            var sr = new RectangleF(Sf(4) + i * segW, Sf(4), segW, Height - Sf(8));
            Color text = Pal.TextDim;
            if (i == selected)
            {
                Draw.Fill(g, En(SelectedColor), sr, Sf(6));
                text = Color.White;
            }
            else if (i == hot && Enabled)
            {
                Draw.Fill(g, Pal.Hover, sr, Sf(6));
                text = Pal.Text;
            }
            Draw.Text(g, items[i], i == selected ? Fonts.BodySemi(DeviceDpi) : Fonts.Body(DeviceDpi),
                Rectangle.Round(sr), En(text),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }
        FocusRing(g, r, Sf(9));
    }
}

enum ButtonKind { Primary, Secondary, Ghost }

/// <summary>Yuvarlak köşeli buton.</summary>
sealed class PillButton : DControl, IPreferred, IButtonControl
{
    public ButtonKind Kind { get; set; } = ButtonKind.Secondary;
    public string IconGlyph { get; set; }
    public Color AccentColor { get; set; } = Pal.Accent;
    public DialogResult DialogResult { get; set; }
    public int MinWidth { get; set; } = 0;

    public PillButton(string text, ButtonKind kind = ButtonKind.Secondary, string icon = null)
    {
        Text = text;
        Kind = kind;
        IconGlyph = icon;
        SetStyle(ControlStyles.Selectable, true);
        TabStop = true;
        Cursor = Cursors.Hand;
    }

    public Size Preferred
    {
        get
        {
            int w = Draw.Measure(Text, Fonts.BodySemi(DeviceDpi)).Width + S(36);
            if (IconGlyph != null) w += S(24);
            return new Size(Math.Max(w, S(MinWidth)), S(38));
        }
    }

    public void NotifyDefault(bool value) { }
    public void PerformClick() { if (Enabled) OnClick(EventArgs.Empty); }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        if (DialogResult != DialogResult.None && FindForm() is Form f) f.DialogResult = DialogResult;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode is Keys.Space or Keys.Enter) PerformClick();
    }

    protected override void Render(Graphics g)
    {
        var r = new RectangleF(0, 0, Width, Height);
        float rad = Sf(8);
        Color fg = Pal.Text;
        switch (Kind)
        {
            case ButtonKind.Primary:
                var bg = pressed ? Pal.Darken(AccentColor, 0.12f) : hover ? Pal.Lighten(AccentColor, 0.1f) : AccentColor;
                Draw.Fill(g, En(bg), r, rad);
                fg = Color.White;
                break;
            case ButtonKind.Secondary:
                Draw.Fill(g, En(pressed ? Pal.Pressed : hover ? Pal.Hover : Pal.SurfaceHi), r, rad);
                Draw.Stroke(g, En(Pal.BorderHi), r, rad);
                break;
            case ButtonKind.Ghost:
                if (hover || pressed) Draw.Fill(g, pressed ? Pal.Pressed : Pal.Hover, r, rad);
                fg = hover ? Pal.Text : Pal.TextDim;
                break;
        }

        var font = Fonts.BodySemi(DeviceDpi);
        int tw = Draw.Measure(Text, font).Width;
        int iw = IconGlyph != null ? S(24) : 0;
        int x = (Width - tw - iw) / 2;
        if (IconGlyph != null)
            Draw.Icon(g, IconGlyph, 10.5f, DeviceDpi, new Rectangle(x, 0, S(18), Height), En(fg));
        Draw.Text(g, Text, font, new Rectangle(x + iw, 0, tw + S(2), Height), En(fg));
        FocusRing(g, r, rad);
    }
}

/// <summary>Yalnızca ikondan oluşan küçük buton (ör. kapat).</summary>
sealed class IconButton : DControl
{
    public string IconGlyph { get; set; }
    public float IconSize { get; set; } = 9f;

    public IconButton(string glyph)
    {
        IconGlyph = glyph;
        Cursor = Cursors.Hand;
    }

    protected override void Render(Graphics g)
    {
        if (hover) Draw.Fill(g, pressed ? Pal.Pressed : Pal.Hover, new RectangleF(0, 0, Width, Height), Sf(6));
        Draw.Icon(g, IconGlyph, IconSize, DeviceDpi, ClientRectangle, hover ? Pal.Text : Pal.TextDim);
    }
}

/// <summary>Açılır seçim kutusu (koyu menü ile).</summary>
sealed class Picker : DControl, IPreferred
{
    readonly ContextMenuStrip menu = Menus.Create();
    public List<(string Id, string Text)> Options { get; } = new();
    public string SelectedId { get; private set; }
    public string Placeholder { get; set; } = "Seçin";
    public event EventHandler SelectedChanged;

    public Picker()
    {
        SetStyle(ControlStyles.Selectable, true);
        TabStop = true;
        Cursor = Cursors.Hand;
    }

    public Size Preferred => new(S(280), S(38));

    public string SelectedText => Options.FirstOrDefault(o => o.Id == SelectedId).Text;

    public void Select(string id) { SelectedId = id; Invalidate(); }

    void Open()
    {
        if (!Enabled) return;
        menu.Items.Clear();
        foreach (var (id, text) in Options)
        {
            var item = new ToolStripMenuItem(text) { Checked = id == SelectedId };
            item.Click += (_, _) => { SelectedId = id; Invalidate(); SelectedChanged?.Invoke(this, EventArgs.Empty); };
            menu.Items.Add(item);
        }
        if (Options.Count == 0) menu.Items.Add(new ToolStripMenuItem("Ses cihazı bulunamadı") { Enabled = false });
        menu.MinimumSize = new Size(Width, 0);
        menu.Show(this, new Point(0, Height + S(4)));
    }

    protected override void OnClick(EventArgs e) { base.OnClick(e); Focus(); Open(); }
    protected override void OnKeyDown(KeyEventArgs e) { base.OnKeyDown(e); if (e.KeyCode is Keys.Space or Keys.Enter or Keys.Down) Open(); }

    protected override void Render(Graphics g)
    {
        var r = new RectangleF(0, 0, Width, Height);
        Draw.Fill(g, En(hover && Enabled ? Pal.Hover : Pal.SurfaceHi), r, Sf(8));
        Draw.Stroke(g, En(Pal.BorderHi), r, Sf(8));
        string text = SelectedText ?? Placeholder;
        Draw.Text(g, text, Fonts.Body(DeviceDpi), new Rectangle(S(12), 0, Width - S(44), Height),
            En(SelectedText != null ? Pal.Text : Pal.TextMuted));
        Draw.Icon(g, Glyph.ChevronDown, 8.5f, DeviceDpi, new Rectangle(Width - S(34), 0, S(24), Height), En(Pal.TextDim));
        FocusRing(g, r, Sf(8));
    }
}

/// <summary>Koyu, yuvarlak köşeli metin kutusu.</summary>
sealed class TextField : DControl, IPreferred
{
    readonly TextBox box = new()
    {
        BorderStyle = BorderStyle.None,
        BackColor = Pal.SurfaceHi,
        ForeColor = Pal.Text,
        MaxLength = 30,
    };

    public TextField()
    {
        Cursor = Cursors.IBeam;
        Controls.Add(box);
        box.GotFocus += (_, _) => Invalidate();
        box.LostFocus += (_, _) => Invalidate();
        box.MouseEnter += (_, _) => { hover = true; Invalidate(); };
        box.MouseLeave += (_, _) => { hover = false; Invalidate(); };
    }

    public override string Text
    {
        get => box?.Text ?? "";
        set { if (box != null) box.Text = value; }
    }

    public Size Preferred => new(S(240), S(38));

    protected override void OnClick(EventArgs e) { base.OnClick(e); box.Focus(); }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        box.Font = Fonts.Body(DeviceDpi);
        int h = box.PreferredHeight;
        box.SetBounds(S(12), (Height - h) / 2, Width - S(24), h);
    }

    protected override void Render(Graphics g)
    {
        var r = new RectangleF(0, 0, Width, Height);
        bool active = box.Focused;
        Draw.Fill(g, Pal.SurfaceHi, r, Sf(8));
        Draw.Stroke(g, active ? Pal.Accent : hover ? Pal.Lighten(Pal.BorderHi, 0.1f) : Pal.BorderHi, r, Sf(8), active ? Sf(1.5f) : 1f);
    }
}

/// <summary>Kısayol tuşu seçme alanı: tıklayıp kombinasyona basılır; tuşlar klavye tuşu gibi gösterilir.</summary>
sealed class HotkeyField : DControl, IPreferred
{
    Hotkey value = new();
    uint liveMods;
    string hint;

    public Hotkey Value
    {
        get => value;
        set { this.value = value ?? new Hotkey(); Invalidate(); }
    }

    public HotkeyField()
    {
        SetStyle(ControlStyles.Selectable, true);
        TabStop = true;
        Cursor = Cursors.Hand;
    }

    public Size Preferred => new(S(240), S(38));

    Rectangle ClearRect => new(Width - S(34), S(5), S(28), Height - S(10));

    protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); Invalidate(); }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (!value.IsEmpty && ClearRect.Contains(e.Location)) { Value = new Hotkey(); return; }
        Focus();
    }

    protected override void OnGotFocus(EventArgs e) { liveMods = 0; hint = null; base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { liveMods = 0; hint = null; base.OnLostFocus(e); }

    static uint Mods(Keys mods)
    {
        uint m = 0;
        if ((mods & Keys.Control) != 0) m |= Native.MOD_CONTROL;
        if ((mods & Keys.Alt) != 0) m |= Native.MOD_ALT;
        if ((mods & Keys.Shift) != 0) m |= Native.MOD_SHIFT;
        if (Native.GetKeyState(0x5B) < 0 || Native.GetKeyState(0x5C) < 0) m |= Native.MOD_WIN;
        return m;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (msg.Msg != 0x100 && msg.Msg != 0x104) return base.ProcessCmdKey(ref msg, keyData); // KEYDOWN / SYSKEYDOWN
        Keys key = keyData & Keys.KeyCode, mods = keyData & Keys.Modifiers;
        if (key == Keys.Tab && mods == Keys.None) return base.ProcessCmdKey(ref msg, keyData);

        hint = null;
        if (key is Keys.ShiftKey or Keys.ControlKey or Keys.Menu or Keys.LWin or Keys.RWin)
        {
            liveMods = Mods(mods);
        }
        else if ((key is Keys.Back or Keys.Delete) && mods == Keys.None)
        {
            value = new Hotkey();
            liveMods = 0;
        }
        else if (key == Keys.Escape && mods == Keys.None)
        {
            liveMods = 0;
            Parent?.Focus();
        }
        else
        {
            uint m = Mods(mods);
            if (m == 0) hint = "Ctrl, Alt veya Shift ile birlikte basın";
            else { value = new Hotkey { Mods = m, Key = (int)key }; liveMods = 0; }
        }
        Invalidate();
        return true;
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        liveMods = Mods(e.Modifiers);
        Invalidate();
        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    static List<string> Parts(uint mods, int key)
    {
        var p = new List<string>();
        if ((mods & Native.MOD_CONTROL) != 0) p.Add("Ctrl");
        if ((mods & Native.MOD_ALT) != 0) p.Add("Alt");
        if ((mods & Native.MOD_SHIFT) != 0) p.Add("Shift");
        if ((mods & Native.MOD_WIN) != 0) p.Add("Win");
        if (key != 0)
        {
            var s = new Hotkey { Mods = 0, Key = key }.ToString();
            p.Add(s);
        }
        return p;
    }

    protected override void Render(Graphics g)
    {
        var r = new RectangleF(0, 0, Width, Height);
        bool active = Focused;
        Draw.Fill(g, active ? Pal.Mix(Pal.SurfaceHi, Pal.Accent, 0.08f) : hover ? Pal.Hover : Pal.SurfaceHi, r, Sf(8));
        Draw.Stroke(g, active ? Pal.Accent : Pal.BorderHi, r, Sf(8), active ? Sf(1.5f) : 1f);

        int cy = Height / 2, x = S(8);
        if (hint != null)
            Draw.Text(g, hint, Fonts.Caption(DeviceDpi), new Rectangle(S(12), 0, Width - S(20), Height), Pal.Warning);
        else if (active && liveMods != 0)
            Draw.KeyChips(g, Parts(liveMods, 0).Append("…"), x, cy, DeviceDpi);
        else if (active)
            Draw.Text(g, "Tuş kombinasyonuna basın…", Fonts.Body(DeviceDpi), new Rectangle(S(12), 0, Width - S(20), Height), Pal.TextDim);
        else if (value.IsEmpty)
            Draw.Text(g, "Atanmamış", Fonts.Body(DeviceDpi), new Rectangle(S(12), 0, Width - S(20), Height), Pal.TextMuted);
        else
        {
            Draw.KeyChips(g, Parts(value.Mods, value.Key), x, cy, DeviceDpi);
            var cr = ClearRect;
            bool overClear = hover && cr.Contains(PointToClient(Cursor.Position));
            if (overClear) Draw.Fill(g, Pal.Pressed, cr, Sf(6));
            if (hover) Draw.Icon(g, Glyph.Close, 8f, DeviceDpi, cr, overClear ? Pal.Text : Pal.TextDim);
        }
    }
}

// =====================================================================
//  Sayfa düzeni: kart, satır, bilgi kutusu, sayfa
// =====================================================================

/// <summary>Kart içinde bir ayar satırı: solda başlık + açıklama, sağda kontrol.</summary>
sealed class SettingRow : DControl
{
    string title, description;
    readonly Control editor;
    public Color? DescriptionColor { get; set; }

    public SettingRow(string title, string description, Control editor = null)
    {
        this.title = title;
        this.description = description;
        this.editor = editor;
        BackColor = Color.Transparent;
        if (editor != null)
        {
            editor.BackColor = Pal.Surface;
            Controls.Add(editor);
            editor.EnabledChanged += (_, _) => Invalidate();
        }
    }

    public string Description
    {
        get => description;
        set { description = value; (Parent as Card)?.Owner?.Relayout(); Invalidate(); }
    }

    public string Value { get; set; } // editor yoksa sağda gösterilecek metin

    int Pad => S(18);

    Size EditorSize => editor is IPreferred p ? p.Preferred : editor?.Size ?? Size.Empty;

    int TextWidth(int width)
    {
        int ew = editor != null ? EditorSize.Width : Value != null ? Draw.Measure(Value, Fonts.Body(DeviceDpi)).Width : 0;
        return width - Pad * 2 - ew - (ew > 0 ? S(20) : 0);
    }

    public int LayoutFor(int width)
    {
        int tw = TextWidth(width);
        int th = Draw.WrappedHeight(title, Fonts.BodySemi(DeviceDpi), tw);
        int dh = Draw.WrappedHeight(description, Fonts.Caption(DeviceDpi), tw);
        int textH = th + (dh > 0 ? dh + S(3) : 0);
        var es = EditorSize;
        int h = Math.Max(textH, es.Height) + S(15) * 2;
        Height = h;
        Width = width;
        editor?.SetBounds(width - Pad - es.Width, (h - es.Height) / 2, es.Width, es.Height);
        return h;
    }

    protected override void Render(Graphics g)
    {
        int tw = TextWidth(Width);
        var fTitle = Fonts.BodySemi(DeviceDpi);
        var fDesc = Fonts.Caption(DeviceDpi);
        int th = Draw.WrappedHeight(title, fTitle, tw);
        int dh = Draw.WrappedHeight(description, fDesc, tw);
        int textH = th + (dh > 0 ? dh + S(3) : 0);
        int y = (Height - textH) / 2;
        bool on = editor?.Enabled ?? true;
        Draw.Wrapped(g, title, fTitle, new Rectangle(Pad, y, tw, th + 2), on ? Pal.Text : Pal.TextMuted);
        if (dh > 0)
            Draw.Wrapped(g, description, fDesc, new Rectangle(Pad, y + th + S(3), tw, dh + 2),
                on ? DescriptionColor ?? Pal.TextDim : Pal.TextMuted);
        if (editor == null && Value != null)
            Draw.Text(g, Value, Fonts.Body(DeviceDpi), new Rectangle(0, 0, Width - Pad, Height), Pal.TextDim,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
    }
}

interface IBlock { int LayoutFor(int width); }

/// <summary>Yuvarlak köşeli yüzey; içindeki satırları alt alta dizer ve aralarına çizgi çeker.</summary>
sealed class Card : DControl, IBlock
{
    public Page Owner { get; set; }
    readonly List<SettingRow> rows = new();

    public Card(params SettingRow[] rows)
    {
        BackColor = Pal.Bg;
        foreach (var r in rows) { this.rows.Add(r); Controls.Add(r); }
    }

    public int LayoutFor(int width)
    {
        Width = width;
        int y = 1;
        foreach (var r in rows)
        {
            int h = r.LayoutFor(width - 2);
            r.Location = new Point(1, y);
            y += h;
        }
        Height = y + 1;
        Invalidate(true);
        return Height;
    }

    protected override void Render(Graphics g)
    {
        var r = new RectangleF(0, 0, Width, Height);
        Draw.Fill(g, Pal.Surface, r, Sf(10));
        Draw.Stroke(g, Pal.Border, r, Sf(10));
        using var pen = new Pen(Pal.Border, 1);
        for (int i = 0; i < rows.Count - 1; i++)
        {
            int y = rows[i].Bottom;
            g.DrawLine(pen, S(18), y, Width - S(18), y);
        }
    }
}

enum CalloutKind { Info, Warning }

/// <summary>Renkli bilgi / uyarı kutusu.</summary>
sealed class Callout : DControl, IBlock
{
    readonly CalloutKind kind;
    readonly string text;

    public Callout(CalloutKind kind, string text)
    {
        this.kind = kind;
        this.text = text;
        BackColor = Pal.Bg;
    }

    Color Tint => kind == CalloutKind.Warning ? Pal.Warning : Pal.Accent;

    public int LayoutFor(int width)
    {
        Width = width;
        Height = Math.Max(S(48), Draw.WrappedHeight(text, Fonts.Caption(DeviceDpi), width - S(62)) + S(28));
        return Height;
    }

    protected override void Render(Graphics g)
    {
        var r = new RectangleF(0, 0, Width, Height);
        Draw.Fill(g, Pal.Mix(Pal.Bg, Tint, 0.09f), r, Sf(10));
        Draw.Stroke(g, Pal.Mix(Pal.Border, Tint, 0.35f), r, Sf(10));
        Draw.Icon(g, kind == CalloutKind.Warning ? Glyph.Warning : Glyph.Info, 12f, DeviceDpi,
            new Rectangle(S(14), S(13), S(22), S(22)), Tint);
        Draw.Wrapped(g, text, Fonts.Caption(DeviceDpi), new Rectangle(S(46), S(14), Width - S(62), Height - S(20)),
            Pal.Mix(Pal.Text, Pal.TextDim, 0.35f));
    }
}

/// <summary>Bir ayar sayfası: başlık, alt başlık ve alt alta bloklar.</summary>
sealed class Page : DControl
{
    public string Title { get; }
    public string Subtitle { get; }
    public string IconGlyph { get; }
    readonly List<Control> blocks = new();

    public Page(string title, string subtitle, string icon)
    {
        Title = title;
        Subtitle = subtitle;
        IconGlyph = icon;
        BackColor = Pal.Bg;
    }

    public void Add(Control block)
    {
        if (block is Card c) c.Owner = this;
        blocks.Add(block);
        Controls.Add(block);
    }

    int HeaderHeight(int w) => S(30) + Draw.Measure(Title, Fonts.Title(DeviceDpi)).Height + S(6) +
                               Draw.WrappedHeight(Subtitle, Fonts.Body(DeviceDpi), w) + S(22);

    public void Relayout()
    {
        int pad = S(32), w = Width - pad * 2;
        if (w <= 0) return;
        int y = HeaderHeight(w);
        foreach (var b in blocks)
        {
            int h = b is IBlock ib ? ib.LayoutFor(w) : b.Height;
            b.Location = new Point(pad, y);
            y += h + S(14);
        }
        Invalidate();
    }

    protected override void OnResize(EventArgs e) { base.OnResize(e); Relayout(); }
    protected override void OnDpiChangedAfterParent(EventArgs e) { base.OnDpiChangedAfterParent(e); Relayout(); }

    protected override void Render(Graphics g)
    {
        int pad = S(32), w = Width - pad * 2;
        var ft = Fonts.Title(DeviceDpi);
        int th = Draw.Measure(Title, ft).Height;
        Draw.Text(g, Title, ft, new Rectangle(pad, S(30), w, th + S(2)), Pal.Text);
        Draw.Wrapped(g, Subtitle, Fonts.Body(DeviceDpi), new Rectangle(pad, S(30) + th + S(6), w, S(60)), Pal.TextDim);
    }
}

// =====================================================================
//  Koyu sağ tık menüsü
// =====================================================================

static class Menus
{
    public static ContextMenuStrip Create()
    {
        var m = new ContextMenuStrip
        {
            Renderer = new DarkMenuRenderer(),
            BackColor = Pal.Surface,
            ForeColor = Pal.Text,
            Font = new Font(Fonts.Text, 10f),
            Padding = new Padding(4, 6, 4, 6),
            ShowImageMargin = true,
            DropShadowEnabled = true,
        };
        m.HandleCreated += (_, _) => Dwm.Rounded(m.Handle, small: false, border: Pal.BorderHi);
        return m;
    }
}

sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
{
    public DarkMenuRenderer() : base(new DarkMenuColors()) { RoundedEdges = false; }

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e) => e.Graphics.Clear(Pal.Surface);

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e) { /* DWM çerçeve çiziyor */ }

    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (!e.Item.Selected || !e.Item.Enabled) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Draw.Fill(e.Graphics, Pal.Hover, new RectangleF(2, 1, e.Item.Width - 4, e.Item.Height - 2), 5);
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        bool isShortcut = e.Item is ToolStripMenuItem mi && e.Text == mi.ShortcutKeyDisplayString;
        e.TextColor = !e.Item.Enabled ? Pal.TextDim : isShortcut ? Pal.TextMuted : Pal.Text;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        int y = e.Item.Height / 2;
        using var pen = new Pen(Pal.Border);
        e.Graphics.DrawLine(pen, 10, y, e.Item.Width - 10, y);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        var r = e.ImageRectangle;
        r.Inflate(4, 4);
        TextRenderer.DrawText(e.Graphics, Glyph.Check, new Font(Fonts.Icons, 9f), r, Pal.Accent,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = Pal.TextDim;
        base.OnRenderArrow(e);
    }
}

sealed class DarkMenuColors : ProfessionalColorTable
{
    public override Color ToolStripDropDownBackground => Pal.Surface;
    public override Color ImageMarginGradientBegin => Pal.Surface;
    public override Color ImageMarginGradientMiddle => Pal.Surface;
    public override Color ImageMarginGradientEnd => Pal.Surface;
    public override Color MenuBorder => Pal.BorderHi;
    public override Color MenuItemBorder => Pal.Hover;
    public override Color MenuItemSelected => Pal.Hover;
    public override Color SeparatorDark => Pal.Border;
    public override Color SeparatorLight => Pal.Border;
    public override Color CheckBackground => Pal.Surface;
    public override Color CheckSelectedBackground => Pal.Hover;
    public override Color CheckPressedBackground => Pal.Hover;
}
