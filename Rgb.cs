namespace KlavyeIsik;

/// <summary>0..1 aralığında renk.</summary>
public readonly struct Rgb
{
    public readonly float R, G, B;

    public Rgb(float r, float g, float b) { R = r; G = g; B = b; }

    public static readonly Rgb Black = new(0, 0, 0);

    public static Rgb Bytes(int r, int g, int b) => new(r / 255f, g / 255f, b / 255f);
    public static Rgb FromColor(Color c) => Bytes(c.R, c.G, c.B);

    public static Rgb operator *(Rgb c, float k) => new(c.R * k, c.G * k, c.B * k);
    public static Rgb operator +(Rgb a, Rgb b) => new(a.R + b.R, a.G + b.G, a.B + b.B);

    public static Rgb Lerp(Rgb a, Rgb b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t);
    }

    public static Rgb Hsv(float h, float s, float v)
    {
        h -= MathF.Floor(h);
        s = Math.Clamp(s, 0f, 1f);
        v = Math.Clamp(v, 0f, 1f);
        float x = h * 6f;
        int i = (int)x;
        float f = x - i;
        float p = v * (1 - s), q = v * (1 - s * f), t = v * (1 - s * (1 - f));
        return (i % 6) switch
        {
            0 => new(v, t, p),
            1 => new(q, v, p),
            2 => new(p, v, t),
            3 => new(p, q, v),
            4 => new(t, p, v),
            _ => new(v, p, q),
        };
    }

    private static int To8(float v) => (int)MathF.Round(Math.Clamp(v, 0f, 1f) * 255f);

    public int Pack() => (To8(R) << 16) | (To8(G) << 8) | To8(B);

    public static Color Unpack(int packed) =>
        Color.FromArgb((packed >> 16) & 0xFF, (packed >> 8) & 0xFF, packed & 0xFF);

    public Color ToColor() => Unpack(Pack());
}
