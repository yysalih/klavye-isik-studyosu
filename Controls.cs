using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace KlavyeIsik;

internal static class Theme
{
    public static readonly Color Bg = Color.FromArgb(15, 16, 20);
    public static readonly Color Panel = Color.FromArgb(22, 24, 30);
    public static readonly Color Panel2 = Color.FromArgb(32, 35, 44);
    public static readonly Color Hover = Color.FromArgb(40, 44, 56);
    public static readonly Color Border = Color.FromArgb(48, 52, 64);
    public static readonly Color Text = Color.FromArgb(232, 234, 242);
    public static readonly Color Sub = Color.FromArgb(140, 146, 166);
    public static readonly Color Accent = Color.FromArgb(118, 92, 255);
    public static readonly Color Track = Color.FromArgb(50, 54, 68);

    public static GraphicsPath Round(RectangleF r, float radius)
    {
        var p = new GraphicsPath();
        float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        if (d <= 0.5f) { p.AddRectangle(r); return p; }
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public static void Fill(Graphics g, Color c, RectangleF r, float radius)
    {
        using var p = Round(r, radius);
        using var b = new SolidBrush(c);
        g.FillPath(b, p);
    }

    public static void Stroke(Graphics g, Color c, RectangleF r, float radius, float width = 1f)
    {
        using var p = Round(r, radius);
        using var pen = new Pen(c, width);
        g.DrawPath(pen, p);
    }

    public static Color Mix(Color a, Color b, float t) => Color.FromArgb(
        (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));

    public static Color WithAlpha(Color c, int a) => Color.FromArgb(Math.Clamp(a, 0, 255), c.R, c.G, c.B);
}

internal abstract class DarkControl : Control
{
    protected bool Hovered;

    protected DarkControl()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Theme.Bg;
        ForeColor = Theme.Text;
        Cursor = Cursors.Hand;
    }

    protected float Dpi => DeviceDpi / 96f;
    protected override void OnMouseEnter(EventArgs e) { Hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { Hovered = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

    protected static Graphics Prep(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        return g;
    }
}

// ------------------------------------------------------------------ Kaydırıcı

internal sealed class DarkSlider : DarkControl
{
    private int _min, _max = 100, _val = 50;
    private bool _drag;
    public event EventHandler? ValueChanged;
    public int Minimum { get => _min; set { _min = value; Invalidate(); } }
    public int Maximum { get => _max; set { _max = value; Invalidate(); } }
    public int SmallStep { get; set; } = 5;

    public DarkSlider() { SetStyle(ControlStyles.Selectable, true); TabStop = true; }

    public int Value
    {
        get => _val;
        set
        {
            int v = Math.Clamp(value, _min, _max);
            if (v == _val) return;
            _val = v;
            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private float Pad => Height / 2f;

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = Prep(e);
        float pad = Pad, y = Height / 2f, th = 4 * Dpi;
        float frac = _max > _min ? (_val - _min) / (float)(_max - _min) : 0;
        float x = pad + frac * (Width - 2 * pad);
        Theme.Fill(g, Theme.Track, new RectangleF(pad, y - th / 2, Width - 2 * pad, th), th / 2);
        Theme.Fill(g, Enabled ? Theme.Accent : Theme.Sub, new RectangleF(pad, y - th / 2, x - pad, th), th / 2);
        float r = Height * (Hovered || _drag ? 0.34f : 0.3f);
        using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, x - r, y - r, 2 * r, 2 * r);
        using (var p = new Pen(Enabled ? Theme.Accent : Theme.Sub, 2 * Dpi)) g.DrawEllipse(p, x - r, y - r, 2 * r, 2 * r);
        if (Focused)
            using (var p = new Pen(Theme.WithAlpha(Theme.Accent, 90), 1)) g.DrawEllipse(p, x - r - 3, y - r - 3, 2 * r + 6, 2 * r + 6);
    }

    private void SetFromX(int x)
    {
        float frac = (x - Pad) / Math.Max(1f, Width - 2 * Pad);
        Value = _min + (int)MathF.Round(Math.Clamp(frac, 0f, 1f) * (_max - _min));
    }

    protected override void OnMouseDown(MouseEventArgs e) { Focus(); _drag = true; Capture = true; SetFromX(e.X); base.OnMouseDown(e); }
    protected override void OnMouseMove(MouseEventArgs e) { if (_drag) SetFromX(e.X); base.OnMouseMove(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _drag = false; Capture = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnMouseWheel(MouseEventArgs e) { Value += Math.Sign(e.Delta) * SmallStep; base.OnMouseWheel(e); }
    protected override bool IsInputKey(Keys k) => k is Keys.Left or Keys.Right or Keys.Up or Keys.Down || base.IsInputKey(k);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Left or Keys.Down) Value -= SmallStep;
        if (e.KeyCode is Keys.Right or Keys.Up) Value += SmallStep;
        base.OnKeyDown(e);
    }

    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
}

// ------------------------------------------------------------------ Aç/kapa anahtarı

internal sealed class Toggle : DarkControl
{
    private bool _on;
    public event EventHandler? CheckedChanged;

    public bool Checked
    {
        get => _on;
        set { if (_on == value) return; _on = value; Invalidate(); CheckedChanged?.Invoke(this, EventArgs.Empty); }
    }

    protected override void OnClick(EventArgs e) { Checked = !Checked; base.OnClick(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = Prep(e);
        float w = 34 * Dpi, h = 18 * Dpi, y = (Height - h) / 2;
        var track = new RectangleF(1, y, w, h);
        Theme.Fill(g, _on ? Theme.Accent : (Hovered ? Theme.Hover : Theme.Track), track, h / 2);
        float k = h - 6 * Dpi;
        float kx = _on ? track.Right - k - 3 * Dpi : track.X + 3 * Dpi;
        using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, kx, y + 3 * Dpi, k, k);
        var tr = new Rectangle((int)(w + 10 * Dpi), 0, Width - (int)(w + 10 * Dpi), Height);
        TextRenderer.DrawText(g, Text, Font, tr, Theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
    }
}

// ------------------------------------------------------------------ Parçalı seçici

internal sealed class Segmented : DarkControl
{
    private int _sel;
    private int _hoverIdx = -1;
    public string[] Items { get; set; } = Array.Empty<string>();
    public event EventHandler? SelectedIndexChanged;

    public int SelectedIndex
    {
        get => _sel;
        set { if (_sel == value) return; _sel = value; Invalidate(); SelectedIndexChanged?.Invoke(this, EventArgs.Empty); }
    }

    private RectangleF Seg(int i)
    {
        float w = (Width - 6f) / Math.Max(1, Items.Length);
        return new RectangleF(3 + i * w, 3, w, Height - 6);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = Prep(e);
        Theme.Fill(g, Theme.Panel2, new RectangleF(0, 0, Width - 1, Height - 1), 8 * Dpi);
        for (int i = 0; i < Items.Length; i++)
        {
            var r = Seg(i);
            if (i == _sel) Theme.Fill(g, Theme.Accent, r, 6 * Dpi);
            else if (i == _hoverIdx) Theme.Fill(g, Theme.Hover, r, 6 * Dpi);
            TextRenderer.DrawText(g, Items[i], Font, Rectangle.Round(r), i == _sel ? Color.White : Theme.Text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    private int HitTest(int x)
    {
        for (int i = 0; i < Items.Length; i++)
            if (Seg(i).Contains(x, Height / 2f)) return i;
        return -1;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        int h = HitTest(e.X);
        if (h != _hoverIdx) { _hoverIdx = h; Invalidate(); }
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e) { _hoverIdx = -1; base.OnMouseLeave(e); }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        int h = HitTest(e.X);
        if (h >= 0) SelectedIndex = h;
        base.OnMouseDown(e);
    }
}

// ------------------------------------------------------------------ Renk kutucukları

internal sealed class Swatch : DarkControl
{
    public Color Color { get; set; }
    public string Tip { get; set; } = "";

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = Prep(e);
        var r = new RectangleF(2, 2, Width - 5, Height - 5);
        if (!Enabled) { Theme.Fill(g, Theme.Mix(Color, Theme.Bg, 0.75f), r, 6 * Dpi); return; }
        Theme.Fill(g, Color, r, 6 * Dpi);
        if (Hovered) Theme.Stroke(g, Color.White, new RectangleF(0.5f, 0.5f, Width - 2, Height - 2), 7 * Dpi, 1.5f * Dpi);
    }
}

internal sealed class ColorChip : DarkControl
{
    private Color _color = Color.DodgerBlue;
    public Color Color { get => _color; set { _color = value; Invalidate(); } }
    public string Caption { get; set; } = "Renk";

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = Prep(e);
        var full = new RectangleF(0.5f, 0.5f, Width - 2, Height - 2);
        Theme.Fill(g, Hovered && Enabled ? Theme.Hover : Theme.Panel2, full, 9 * Dpi);
        Theme.Stroke(g, Theme.Border, full, 9 * Dpi);
        float s = Height - 14 * Dpi;
        var sw = new RectangleF(7 * Dpi, 7 * Dpi, s, s);
        Theme.Fill(g, Enabled ? _color : Theme.Mix(_color, Theme.Panel2, 0.8f), sw, 6 * Dpi);
        int tx = (int)(sw.Right + 8 * Dpi);
        var c1 = Enabled ? Theme.Text : Theme.Sub;
        TextRenderer.DrawText(g, Caption, Font, new Rectangle(tx, (int)(5 * Dpi), Width - tx, Height / 2), c1, TextFormatFlags.Left);
        using var small = new Font(Font.FontFamily, Font.Size - 1f);
        TextRenderer.DrawText(g, Enabled ? AppSettings.ToHex(_color) : "kullanılmıyor", small,
            new Rectangle(tx, Height / 2, Width - tx, Height / 2 - 2), Theme.Sub, TextFormatFlags.Left);
    }
}

// ------------------------------------------------------------------ Hap düğme

internal sealed class PillButton : DarkControl
{
    private bool _active;
    public bool Active { get => _active; set { _active = value; Invalidate(); } }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = Prep(e);
        var r = new RectangleF(0.5f, 0.5f, Width - 2, Height - 2);
        var fill = _active ? Theme.Accent : Theme.Panel2;
        if (Hovered) fill = Theme.Mix(fill, Color.White, 0.08f);
        Theme.Fill(g, fill, r, r.Height / 2);
        if (!_active) Theme.Stroke(g, Theme.Border, r, r.Height / 2);
        float d = 8 * Dpi;
        using (var b = new SolidBrush(_active ? Color.FromArgb(120, 255, 160) : Color.FromArgb(255, 90, 90)))
            g.FillEllipse(b, 14 * Dpi, (Height - d) / 2, d, d);
        TextRenderer.DrawText(g, Text, Font, new Rectangle((int)(28 * Dpi), 0, Width - (int)(34 * Dpi), Height),
            _active ? Color.White : Theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
    }
}

// ------------------------------------------------------------------ Efekt listesi

internal sealed class EffectList : ListBox
{
    private int _hover = -1;
    private readonly Font _headFont;
    private readonly Font _itemFont;

    public EffectList()
    {
        DrawMode = DrawMode.OwnerDrawVariable;
        BorderStyle = BorderStyle.None;
        BackColor = Theme.Panel;
        ForeColor = Theme.Text;
        IntegralHeight = false;
        _headFont = new Font("Segoe UI Semibold", 7.75f);
        _itemFont = new Font("Segoe UI", 9.75f);
        SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
    }

    private float Dpi => DeviceDpi / 96f;

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Native.SetWindowTheme(Handle, "DarkMode_Explorer", null);
    }

    protected override void OnMeasureItem(MeasureItemEventArgs e)
    {
        e.ItemHeight = (int)((Items[e.Index] is string ? (e.Index == 0 ? 26 : 34) : 34) * Dpi);
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var bg = new SolidBrush(Theme.Panel)) g.FillRectangle(bg, e.Bounds);
        var item = Items[e.Index];
        if (item is string head)
        {
            var r = new Rectangle(e.Bounds.X + (int)(14 * Dpi), e.Bounds.Bottom - (int)(20 * Dpi), e.Bounds.Width, (int)(18 * Dpi));
            TextRenderer.DrawText(g, head, _headFont, r, Theme.Sub, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            return;
        }
        if (item is not Effect eff) return;
        bool sel = (e.State & DrawItemState.Selected) != 0;
        var rr = new RectangleF(e.Bounds.X + 6 * Dpi, e.Bounds.Y + 2 * Dpi, e.Bounds.Width - 12 * Dpi, e.Bounds.Height - 4 * Dpi);
        if (sel) Theme.Fill(g, Theme.Mix(Theme.Panel, Theme.Accent, 0.35f), rr, 8 * Dpi);
        else if (e.Index == _hover) Theme.Fill(g, Theme.Hover, rr, 8 * Dpi);
        if (sel) Theme.Fill(g, Theme.Accent, new RectangleF(rr.X, rr.Y + 7 * Dpi, 3 * Dpi, rr.Height - 14 * Dpi), 1.5f * Dpi);

        float d = 14 * Dpi;
        var dot = new RectangleF(rr.X + 12 * Dpi, rr.Y + (rr.Height - d) / 2, d, d);
        using (var lg = new LinearGradientBrush(dot, eff.Dot1.ToColor(), eff.Dot2.ToColor(), 45f))
            g.FillEllipse(lg, dot);

        var tr = new Rectangle((int)(dot.Right + 10 * Dpi), e.Bounds.Y, e.Bounds.Width - (int)(dot.Right + 14 * Dpi), e.Bounds.Height);
        TextRenderer.DrawText(g, eff.Name, _itemFont, tr, sel ? Color.White : Theme.Text,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        if (eff.Kind == Kind.Static)
            TextRenderer.DrawText(g, "%0 CPU", _headFont, new Rectangle(e.Bounds.Right - (int)(70 * Dpi), e.Bounds.Y, (int)(60 * Dpi), e.Bounds.Height),
                Color.FromArgb(120, 220, 150), TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
    }

    protected override void OnSelectedIndexChanged(EventArgs e)
    {
        if (SelectedIndex >= 0 && Items[SelectedIndex] is string && SelectedIndex + 1 < Items.Count)
        {
            SelectedIndex += 1;
            return;
        }
        base.OnSelectedIndexChanged(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        int i = IndexFromPoint(e.Location);
        if (i != _hover)
        {
            int old = _hover;
            _hover = i;
            if (old >= 0 && old < Items.Count) Invalidate(GetItemRectangle(old));
            if (i >= 0 && i < Items.Count) Invalidate(GetItemRectangle(i));
        }
        Cursor = i >= 0 && i < Items.Count && Items[i] is Effect ? Cursors.Hand : Cursors.Default;
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        if (_hover >= 0 && _hover < Items.Count) Invalidate(GetItemRectangle(_hover));
        _hover = -1;
        base.OnMouseLeave(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _headFont.Dispose(); _itemFont.Dispose(); }
        base.Dispose(disposing);
    }
}

// ------------------------------------------------------------------ Canlı klavye önizlemesi

/// <summary>
/// Klavye çizimi iki katmana önceden çizilir (koyu taban + beyaz ışık katmanı);
/// her karede sadece ışık katmanı tek bir ColorMatrix ile renklendirilir. Ucuz ve akıcı.
/// </summary>
internal sealed class KeyboardPreview : Control
{
    private Bitmap? _base, _light;
    private Color _color = Color.Black;
    private readonly ImageAttributes _attr = new();
    private readonly float[][] _m =
    {
        new float[] { 1, 0, 0, 0, 0 }, new float[] { 0, 1, 0, 0, 0 }, new float[] { 0, 0, 1, 0, 0 },
        new float[] { 0, 0, 0, 1, 0 }, new float[] { 0, 0, 0, 0, 1 },
    };

    private static readonly (float w, string label)[][] Rows =
    {
        new[] { (1f, "Esc"), (1f, "F1"), (1f, "F2"), (1f, "F3"), (1f, "F4"), (1f, "F5"), (1f, "F6"), (1f, "F7"), (1f, "F8"), (1f, "F9"), (1f, "F10"), (1f, "F11"), (1f, "F12"), (1f, "Ins"), (1f, "Del") },
        new[] { (1f, "\""), (1f, "1"), (1f, "2"), (1f, "3"), (1f, "4"), (1f, "5"), (1f, "6"), (1f, "7"), (1f, "8"), (1f, "9"), (1f, "0"), (1f, "*"), (1f, "-"), (2f, "⌫") },
        new[] { (1.5f, "Tab"), (1f, "Q"), (1f, "W"), (1f, "E"), (1f, "R"), (1f, "T"), (1f, "Y"), (1f, "U"), (1f, "I"), (1f, "O"), (1f, "P"), (1f, "Ğ"), (1f, "Ü"), (1.5f, ",") },
        new[] { (1.75f, "Caps"), (1f, "A"), (1f, "S"), (1f, "D"), (1f, "F"), (1f, "G"), (1f, "H"), (1f, "J"), (1f, "K"), (1f, "L"), (1f, "Ş"), (1f, "İ"), (2.25f, "Enter") },
        new[] { (2.25f, "Shift"), (1f, "Z"), (1f, "X"), (1f, "C"), (1f, "V"), (1f, "B"), (1f, "N"), (1f, "M"), (1f, "Ö"), (1f, "Ç"), (1f, "."), (2.75f, "Shift") },
        new[] { (1.25f, "Ctrl"), (1.25f, "Fn"), (1.25f, "⊞"), (1.25f, "Alt"), (5.5f, ""), (1.25f, "AltGr"), (1.25f, "Ctrl"), (1f, "◀"), (1f, "▶") },
    };

    public KeyboardPreview()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Bg;
    }

    public Color Color
    {
        get => _color;
        set { if (_color.ToArgb() == value.ToArgb()) return; _color = value; Invalidate(); }
    }

    protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); BuildLayers(); }

    private void BuildLayers()
    {
        _base?.Dispose(); _light?.Dispose(); _base = null; _light = null;
        if (Width < 50 || Height < 50) return;
        float dpi = DeviceDpi / 96f;
        _base = new Bitmap(Width, Height, PixelFormat.Format32bppPArgb);
        _light = new Bitmap(Width, Height, PixelFormat.Format32bppPArgb);
        using var gb = Graphics.FromImage(_base);
        using var gl = Graphics.FromImage(_light);
        foreach (var g in new[] { gb, gl })
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        }
        gb.Clear(Theme.Bg);

        var deck = new RectangleF(4, 4, Width - 9, Height - 9);
        Theme.Fill(gb, Color.FromArgb(24, 26, 32), deck, 16 * dpi);
        Theme.Stroke(gb, Theme.Border, deck, 16 * dpi);

        // Klavyenin arkasındaki genel parıltı
        using (var path = new GraphicsPath())
        {
            var glow = RectangleF.Inflate(deck, -deck.Width * 0.05f, -deck.Height * 0.08f);
            path.AddEllipse(glow);
            using var pgb = new PathGradientBrush(path)
            {
                CenterColor = Color.FromArgb(70, 255, 255, 255),
                SurroundColors = new[] { Color.FromArgb(0, 255, 255, 255) },
            };
            gl.FillPath(pgb, path);
        }

        float pad = 16 * dpi;
        var area = RectangleF.Inflate(deck, -pad, -pad);
        float unit = area.Width / 15f;
        float rowH = area.Height / (Rows.Length - 0.35f);
        float gap = Math.Max(2f, unit * 0.09f);
        using var font = new Font("Segoe UI Semibold", Math.Max(6f, unit * 0.21f / dpi));
        using var keyBrush = new SolidBrush(Color.FromArgb(34, 36, 44));
        using var keyEdge = new Pen(Color.FromArgb(46, 49, 60), 1);
        using var lightEdge = new Pen(Color.FromArgb(235, 255, 255, 255), Math.Max(1.2f, 1.6f * dpi));
        using var halo = new Pen(Color.FromArgb(70, 255, 255, 255), Math.Max(3f, 5f * dpi));
        using var legend = new SolidBrush(Color.FromArgb(255, 255, 255, 255));
        using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

        float y = area.Y;
        for (int ri = 0; ri < Rows.Length; ri++)
        {
            float h = ri == 0 ? rowH * 0.65f : rowH;
            float x = area.X;
            foreach (var (w, label) in Rows[ri])
            {
                var kr = new RectangleF(x + gap / 2, y + gap / 2, unit * w - gap, h - gap);
                float rad = Math.Min(kr.Height, kr.Width) * 0.16f;
                using (var p = Theme.Round(kr, rad))
                {
                    gb.FillPath(keyBrush, p);
                    gb.DrawPath(keyEdge, p);
                    gl.DrawPath(halo, p);
                    gl.DrawPath(lightEdge, p);
                }
                if (label.Length > 0)
                {
                    var f = label.Length > 2 ? font : font;
                    gl.DrawString(label, f, legend, kr, sf);
                }
                x += unit * w;
            }
            y += h;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        if (_base == null || _light == null) { g.Clear(Theme.Bg); return; }
        g.CompositingQuality = CompositingQuality.HighSpeed;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.DrawImageUnscaled(_base, 0, 0);

        float r = _color.R / 255f, gg = _color.G / 255f, b = _color.B / 255f;
        float inten = Math.Max(r, Math.Max(gg, b));
        if (inten > 0.004f)
        {
            _m[0][0] = r; _m[1][1] = gg; _m[2][2] = b; _m[3][3] = Math.Min(1f, 0.25f + inten);
            _attr.SetColorMatrix(new ColorMatrix(_m));
            g.DrawImage(_light, new Rectangle(0, 0, _light.Width, _light.Height), 0, 0, _light.Width, _light.Height, GraphicsUnit.Pixel, _attr);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _base?.Dispose(); _light?.Dispose(); _attr.Dispose(); }
        base.Dispose(disposing);
    }
}

// ------------------------------------------------------------------ Koyu tepsi menüsü

internal sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
{
    public DarkMenuRenderer() : base(new DarkColors()) { RoundedEdges = false; }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? Theme.Text : Theme.Sub;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = Theme.Sub;
        base.OnRenderArrow(e);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        var r = e.ImageRectangle;
        r.Inflate(-2, -2);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var b = new SolidBrush(Theme.Accent);
        e.Graphics.FillEllipse(b, r.X + r.Width / 4f, r.Y + r.Height / 4f, r.Width / 2f, r.Height / 2f);
    }

    private sealed class DarkColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Theme.Panel;
        public override Color ImageMarginGradientBegin => Theme.Panel;
        public override Color ImageMarginGradientMiddle => Theme.Panel;
        public override Color ImageMarginGradientEnd => Theme.Panel;
        public override Color MenuBorder => Theme.Border;
        public override Color MenuItemBorder => Theme.Hover;
        public override Color MenuItemSelected => Theme.Hover;
        public override Color MenuItemSelectedGradientBegin => Theme.Hover;
        public override Color MenuItemSelectedGradientEnd => Theme.Hover;
        public override Color SeparatorDark => Theme.Border;
        public override Color SeparatorLight => Theme.Border;
        public override Color CheckBackground => Theme.Panel;
        public override Color CheckSelectedBackground => Theme.Hover;
        public override Color CheckPressedBackground => Theme.Hover;
    }
}

internal static class AppIcon
{
    /// <summary>Simge: renkli tuşlu küçük bir klavye.</summary>
    public static Icon Create(int size = 32)
    {
        using var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            float s = size / 32f;
            var body = new RectangleF(1 * s, 6 * s, 30 * s, 20 * s);
            Theme.Fill(g, Color.FromArgb(28, 30, 38), body, 5 * s);
            Color[] cols = { Color.FromArgb(255, 40, 120), Color.FromArgb(160, 60, 255), Color.FromArgb(0, 150, 255), Color.FromArgb(0, 230, 200) };
            for (int row = 0; row < 3; row++)
                for (int col = 0; col < 4; col++)
                {
                    var kr = new RectangleF((4 + col * 6.3f) * s, (9 + row * 5.3f) * s, 5 * s, 4 * s);
                    if (row == 2 && col is 1 or 2) kr = new RectangleF((4 + 6.3f) * s, kr.Y, (5 + 6.3f) * s, 4 * s);
                    if (row == 2 && col == 2) continue;
                    Theme.Fill(g, cols[(col + row) % cols.Length], kr, 1.2f * s);
                }
        }
        return Icon.FromHandle(bmp.GetHicon());
    }
}
