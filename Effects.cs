namespace KlavyeIsik;

[Flags]
public enum Needs { None = 0, Keys = 1, Audio = 2, Cpu = 4, Battery = 8, Screen = 16 }

/// <summary>Static: bir kez gönderilir, sonra uyur. Slow: birkaç saniyede bir. Animated: kare kare.</summary>
public enum Kind { Static, Animated, Slow }

public delegate Rgb RenderFn(EffectContext c);

/// <summary>Efektlere her karede verilen bilgiler. Tek örnek, tekrar kullanılır (çöp üretmez).</summary>
public sealed class EffectContext
{
    public float T;        // hız ile ölçeklenmiş zaman (sn)
    public float Dt;       // hız ile ölçeklenmiş kare süresi
    public float RealDt;   // gerçek kare süresi
    public Rgb C1, C2;
    public int NewKeys;    // son kareden beri basılan tuş sayısı
    public float Audio;    // 0..1 ses zarfı
    public float Cpu;      // 0..1 işlemci yükü
    public float Battery = 1;
    public bool Charging, OnAc = true;
    public Rgb Screen;     // ekranın baskın rengi (parlaklık dahil)
    public bool ScreenOk;
    public DateTime Now;
    public readonly float[] S = new float[16];   // efektlerin kendi durum alanı
    private readonly Random _rng = new();

    public float Rand() => (float)_rng.NextDouble();
    public void Reset() => Array.Clear(S);
}

public sealed class Effect
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Category { get; init; }
    public required string Description { get; init; }
    public required RenderFn Render { get; init; }
    public Kind Kind { get; init; } = Kind.Animated;
    public Needs Needs { get; init; }
    /// <summary>Kullandığı kullanıcı rengi sayısı: 0, 1 veya 2.</summary>
    public int Colors { get; init; }
    /// <summary>0 = performans ayarına uyar; aksi halde bu FPS'i geçmez.</summary>
    public int MaxFps { get; init; }
    public int SlowMs { get; init; } = 1000;
    /// <summary>Ekran örneklemede kullanılacak alt kısım oranı (1 = tüm ekran).</summary>
    public float ScreenPart { get; init; } = 1f;
    public Rgb Dot1 { get; init; }
    public Rgb Dot2 { get; init; }
    public override string ToString() => Name;
}

public static class EffectCatalog
{
    private const float Tau = MathF.PI * 2f;

    private static Rgb B(int r, int g, int b) => Rgb.Bytes(r, g, b);
    private static float Wave(float t, float period) => 0.5f - 0.5f * MathF.Cos(Tau * t / period);
    private static float S01(float x) => 0.5f + 0.5f * MathF.Sin(x);
    private static float Frac(float x) => x - MathF.Floor(x);
    private static float Sm(float x) { x = Math.Clamp(x, 0f, 1f); return x * x * (3 - 2 * x); }
    private static float Gauss(float x, float c, float w) { float d = (x - c) / w; return MathF.Exp(-d * d); }

    private static float HueLerp(float a, float b, float k)
    {
        float d = b - a;
        if (d > 0.5f) d -= 1;
        if (d < -0.5f) d += 1;
        return Frac(a + d * k);
    }

    private static Rgb Pal(Rgb[] p, float t, float hold, float fade)
    {
        float seg = hold + fade;
        float pos = t / seg;
        int i = (int)MathF.Floor(pos);
        float w = (pos - i) * seg;
        int n = p.Length;
        i = ((i % n) + n) % n;
        return w < hold ? p[i] : Rgb.Lerp(p[i], p[(i + 1) % n], Sm((w - hold) / fade));
    }

    private static readonly Rgb[] FlowPal =
        { B(255, 0, 80), B(170, 0, 255), B(0, 90, 255), B(0, 220, 255), B(0, 255, 120), B(255, 220, 0), B(255, 100, 0) };
    private static readonly Rgb[] NeonPal = { B(255, 0, 140), B(0, 230, 255), B(150, 0, 255) };
    private static readonly Rgb[] SunsetPal = { B(255, 94, 19), B(255, 30, 90), B(150, 20, 170), B(255, 165, 25) };
    private static readonly Rgb[] PastelPal = { B(255, 140, 200), B(190, 140, 255), B(130, 195, 255), B(140, 255, 205), B(255, 215, 140) };
    private static readonly Rgb[] LavenderPal = { B(150, 90, 255), B(255, 90, 165), B(80, 45, 210), B(200, 120, 255) };

    private static readonly (float h, Rgb c)[] DayKeys =
    {
        (0f, B(255, 70, 10) * 0.25f), (5.5f, B(255, 70, 10) * 0.3f), (7f, B(255, 140, 60) * 0.7f),
        (9f, B(255, 210, 160)), (12f, B(210, 235, 255)), (16f, B(255, 235, 200)),
        (18.5f, B(255, 120, 40)), (20.5f, B(200, 50, 130) * 0.75f), (22.5f, B(255, 70, 10) * 0.35f),
        (24f, B(255, 70, 10) * 0.25f),
    };

    public const string CatBasic = "TEMEL";
    public const string CatShow = "RENK ŞOVU";
    public const string CatNature = "DOĞA & ATMOSFER";
    public const string CatReactive = "ETKİLEŞİMLİ";
    public const string CatSystem = "SİSTEM";

    public static readonly Effect[] All =
    {
        // ------------------------------------------------------------ TEMEL
        new()
        {
            Id = "sabit", Name = "Sabit Renk", Category = CatBasic, Kind = Kind.Static, Colors = 1,
            Description = "Tek renk, hiç kıpırdamaz. Klavyeye bir kez gönderilir, sonra program tamamen uyur (%0 işlemci).",
            Dot1 = B(0, 140, 255), Dot2 = B(0, 140, 255),
            Render = c => c.C1,
        },
        new()
        {
            Id = "nefes", Name = "Nefes", Category = CatBasic, Colors = 1,
            Description = "Renk 1 yavaşça yanıp söner, nefes alır gibi.",
            Dot1 = B(0, 140, 255), Dot2 = B(0, 30, 70),
            Render = c => c.C1 * (0.03f + 0.97f * MathF.Pow(Wave(c.T, 4f), 1.6f)),
        },
        new()
        {
            Id = "ikirenk", Name = "İki Renk Geçişi", Category = CatBasic, Colors = 2,
            Description = "Renk 1 ile Renk 2 arasında yumuşak gidip gelir.",
            Dot1 = B(0, 140, 255), Dot2 = B(255, 0, 120),
            Render = c => Rgb.Lerp(c.C1, c.C2, Wave(c.T, 6f)),
        },
        new()
        {
            Id = "kalp", Name = "Kalp Atışı", Category = CatBasic, Colors = 1,
            Description = "Çift vuruşlu kalp ritmi (Renk 1). Kırmızı ile harika görünür.",
            Dot1 = B(255, 0, 30), Dot2 = B(90, 0, 10),
            Render = c =>
            {
                float p = c.T % 1.3f;
                float beat = Gauss(p, 0.12f, 0.08f) + 0.65f * Gauss(p, 0.40f, 0.09f);
                return c.C1 * (0.06f + 0.94f * Math.Min(1f, beat));
            },
        },
        new()
        {
            Id = "flas", Name = "Yanıp Sönme", Category = CatBasic, Colors = 1,
            Description = "Renk 1 ile sert açılıp kapanır.",
            Dot1 = B(255, 255, 255), Dot2 = B(30, 30, 30),
            Render = c => (c.T % 0.9f) < 0.45f ? c.C1 : Rgb.Black,
        },

        // ------------------------------------------------------------ RENK ŞOVU
        new()
        {
            Id = "gokkusagi", Name = "Gökkuşağı", Category = CatShow,
            Description = "Tüm renk çemberinde kesintisiz, akıcı dönüş.",
            Dot1 = B(255, 0, 0), Dot2 = B(0, 120, 255),
            Render = c => Rgb.Hsv(c.T / 12f, 1, 1),
        },
        new()
        {
            Id = "akis", Name = "Renk Akışı", Category = CatShow,
            Description = "Seçilmiş canlı renkler arasında durup-geçerek akar: pembe → mor → mavi → turkuaz → yeşil → sarı → turuncu.",
            Dot1 = B(255, 0, 80), Dot2 = B(0, 220, 255),
            Render = c => Pal(FlowPal, c.T, 1.6f, 1.4f),
        },
        new()
        {
            Id = "rastgele", Name = "Rastgele Renkler", Category = CatShow,
            Description = "Her birkaç saniyede bir sürpriz renge yumuşakça geçer.",
            Dot1 = B(255, 200, 0), Dot2 = B(140, 0, 255),
            Render = c =>
            {
                if (c.S[3] == 0) { c.S[0] = c.Rand(); c.S[1] = c.S[0]; c.S[3] = 1; c.S[2] = 3.5f; }
                c.S[2] += c.Dt;
                if (c.S[2] >= 3.5f) { c.S[2] -= 3.5f; c.S[0] = c.S[1]; c.S[1] = Frac(c.S[0] + 0.2f + 0.6f * c.Rand()); }
                return Rgb.Hsv(HueLerp(c.S[0], c.S[1], Sm((c.S[2] - 2.4f) / 1.1f)), 1, 1);
            },
        },
        new()
        {
            Id = "neon", Name = "Neon Şehir", Category = CatShow,
            Description = "Pembe, camgöbeği ve mor neon tabelalar; arada bir neon cızırtısı gibi titrer.",
            Dot1 = B(255, 0, 140), Dot2 = B(0, 230, 255),
            Render = c =>
            {
                var col = Pal(NeonPal, c.T, 2.2f, 1.0f);
                c.S[0] -= c.Dt;
                if (c.S[0] <= 0) { c.S[0] = 2.5f + 5f * c.Rand(); c.S[1] = 0.3f; }
                if (c.S[1] > 0)
                {
                    c.S[1] -= c.Dt;
                    return (c.S[1] % 0.1f) < 0.05f ? col * 0.3f : col;
                }
                return col;
            },
        },
        new()
        {
            Id = "gunbatimi", Name = "Gün Batımı", Category = CatShow,
            Description = "Turuncu, mercan, mor ve altın tonlarında sakin geçişler.",
            Dot1 = B(255, 94, 19), Dot2 = B(150, 20, 170),
            Render = c => Pal(SunsetPal, c.T, 2.5f, 3.5f),
        },
        new()
        {
            Id = "pastel", Name = "Pastel Rüya", Category = CatShow,
            Description = "Pembe, lila, bebek mavisi, nane yeşili ve şeftali arasında çok yavaş, yumuşacık geçişler. Sakin, göz yormaz.",
            Dot1 = B(255, 140, 200), Dot2 = B(130, 195, 255),
            Render = c => Pal(PastelPal, c.T, 4f, 5f),
        },
        new()
        {
            Id = "lavanta", Name = "Lavanta Akşamı", Category = CatShow,
            Description = "Lavanta, gül kurusu ve çivit mavisi; hafifçe nefes alıyormuş gibi kabarıp söner. Gece için huzurlu.",
            Dot1 = B(150, 90, 255), Dot2 = B(255, 90, 165),
            Render = c => Pal(LavenderPal, c.T, 4f, 6f) * (0.7f + 0.3f * S01(c.T * 0.45f)),
        },
        new()
        {
            Id = "zen", Name = "Zen Akışı", Category = CatShow,
            Description = "Turkuazdan maviye, oradan mora neredeyse fark edilmeyecek kadar yavaş süzülür. Odaklanmak için.",
            Dot1 = B(0, 200, 200), Dot2 = B(90, 70, 255),
            Render = c =>
            {
                float h = 0.47f + 0.26f * S01(c.T * 0.07f) + 0.03f * MathF.Sin(c.T * 0.23f);
                return Rgb.Hsv(h, 0.8f, 0.78f + 0.12f * S01(c.T * 0.3f));
            },
        },
        new()
        {
            Id = "disko", Name = "Disko", Category = CatShow,
            Description = "Hızlı, vuruşlu, rastgele renk patlamaları.",
            Dot1 = B(255, 0, 200), Dot2 = B(0, 255, 100),
            Render = c =>
            {
                float seg = c.T / 0.45f;
                int i = (int)seg;
                float f = seg - i;
                float h = Frac(i * 0.381966f + 0.5f * Frac(MathF.Sin(i * 12.9898f) * 43758.5453f));
                return Rgb.Hsv(h, 1, 1 - 0.55f * f);
            },
        },
        new()
        {
            Id = "polis", Name = "Polis Çakarı", Category = CatShow,
            Description = "Kırmızı-mavi çift çakar.",
            Dot1 = B(255, 0, 0), Dot2 = B(0, 40, 255),
            Render = c =>
            {
                float p = c.T % 1f;
                bool red = p < 0.14f || (p >= 0.2f && p < 0.34f);
                bool blue = (p >= 0.5f && p < 0.64f) || (p >= 0.7f && p < 0.84f);
                return red ? B(255, 0, 0) : blue ? B(0, 40, 255) : Rgb.Black;
            },
        },

        // ------------------------------------------------------------ DOĞA & ATMOSFER
        new()
        {
            Id = "okyanus", Name = "Okyanus", Category = CatNature,
            Description = "Turkuaz ile derin mavi arasında dalgalanan su ışığı.",
            Dot1 = B(0, 220, 255), Dot2 = B(0, 60, 200),
            Render = c =>
            {
                float h = 0.52f + 0.07f * MathF.Sin(c.T * 0.45f) + 0.03f * MathF.Sin(c.T * 1.3f + 1f);
                float v = 0.6f + 0.28f * MathF.Sin(c.T * 0.8f) + 0.12f * MathF.Sin(c.T * 2.3f + 2f);
                return Rgb.Hsv(h, 0.85f + 0.15f * S01(c.T * 0.6f), v);
            },
        },
        new()
        {
            Id = "aurora", Name = "Kuzey Işıkları", Category = CatNature,
            Description = "Yeşil, turkuaz, mavi ve mor arasında süzülen aurora.",
            Dot1 = B(0, 255, 120), Dot2 = B(150, 0, 255),
            Render = c =>
            {
                float t1 = S01(c.T * 0.33f), t2 = S01(c.T * 0.87f + 2f);
                float h = 0.33f + 0.45f * (0.7f * t1 + 0.3f * t2);
                return Rgb.Hsv(h, 0.9f, 0.55f + 0.45f * S01(c.T * 1.1f + 1f));
            },
        },
        new()
        {
            Id = "lav", Name = "Lav Lambası", Category = CatNature,
            Description = "Kırmızı-turuncu, ağır ağır kabaran sıcak ışık.",
            Dot1 = B(255, 30, 0), Dot2 = B(255, 120, 0),
            Render = c =>
            {
                float h = 0.015f + 0.055f * S01(c.T * 0.55f);
                return Rgb.Hsv(h, 1, 0.5f + 0.5f * S01(c.T * 0.9f) * S01(c.T * 0.37f + 1f));
            },
        },
        new()
        {
            Id = "mum", Name = "Mum Işığı", Category = CatNature,
            Description = "Sıcak sarı, gerçekçi titreyen mum alevi. Gece için çok rahat.",
            Dot1 = B(255, 150, 40), Dot2 = B(120, 50, 0),
            Render = c =>
            {
                c.S[2] -= c.Dt;
                if (c.S[2] <= 0)
                {
                    c.S[1] = 0.6f + 0.4f * c.Rand();
                    if (c.Rand() < 0.1f) c.S[1] *= 0.55f;
                    c.S[2] = 0.04f + 0.14f * c.Rand();
                }
                c.S[0] += (c.S[1] - c.S[0]) * (1 - MathF.Exp(-c.Dt * 16));
                return B(255, 120, 25) * c.S[0];
            },
        },
        new()
        {
            Id = "ates", Name = "Ateş", Category = CatNature,
            Description = "Kırmızı-turuncu, hareketli şömine ateşi.",
            Dot1 = B(255, 60, 0), Dot2 = B(255, 160, 0),
            Render = c =>
            {
                c.S[2] -= c.Dt;
                if (c.S[2] <= 0) { c.S[1] = 0.45f + 0.55f * c.Rand(); c.S[3] = 0.075f * c.Rand(); c.S[2] = 0.05f + 0.1f * c.Rand(); }
                float k = 1 - MathF.Exp(-c.Dt * 12);
                c.S[0] += (c.S[1] - c.S[0]) * k;
                c.S[4] += (c.S[3] - c.S[4]) * k;
                return Rgb.Hsv(c.S[4], 1, c.S[0]);
            },
        },
        new()
        {
            Id = "firtina", Name = "Fırtına", Category = CatNature,
            Description = "Koyu lacivert gece, rastgele çakan çift şimşekler.",
            Dot1 = B(200, 220, 255), Dot2 = B(10, 20, 70),
            Render = c =>
            {
                c.S[0] -= c.Dt;
                if (c.S[0] <= 0) { c.S[1] = 0; c.S[0] = 1.5f + 5f * c.Rand(); }
                c.S[1] += c.Dt;
                float a = c.S[1];
                float f = MathF.Exp(-a * 10) + (a > 0.14f ? 0.7f * MathF.Exp(-(a - 0.14f) * 8) : 0);
                return Rgb.Lerp(B(5, 12, 45), B(215, 225, 255), f);
            },
        },
        new()
        {
            Id = "matrix", Name = "Matrix", Category = CatNature,
            Description = "Yeşil dijital yağmur gibi düzensiz parlayan nabızlar.",
            Dot1 = B(0, 255, 60), Dot2 = B(0, 40, 10),
            Render = c =>
            {
                c.S[0] -= c.Dt;
                if (c.S[0] <= 0) { c.S[0] = 0.25f + 1.2f * c.Rand(); c.S[1] = 0; c.S[2] = 0.5f + 0.5f * c.Rand(); }
                c.S[1] += c.Dt;
                float p = c.S[2] * MathF.Exp(-c.S[1] * 3.5f);
                var g = B(0, 255, 60);
                var col = Rgb.Lerp(g * 0.12f, g, p);
                return p > 0.85f ? Rgb.Lerp(col, B(180, 255, 190), (p - 0.85f) / 0.15f) : col;
            },
        },
        new()
        {
            Id = "galaksi", Name = "Galaksi", Category = CatNature,
            Description = "Derin mor-lacivert uzay, arada parıldayan yıldızlar.",
            Dot1 = B(110, 0, 160), Dot2 = B(25, 0, 110),
            Render = c =>
            {
                var baseC = Rgb.Lerp(B(25, 0, 110), B(110, 0, 160), Wave(c.T, 9f)) * (0.55f + 0.25f * S01(c.T * 0.7f));
                c.S[0] -= c.Dt;
                if (c.S[0] <= 0) { c.S[0] = 0.6f + 2.2f * c.Rand(); c.S[1] = 0; c.S[2] = 0.4f + 0.6f * c.Rand(); }
                c.S[1] += c.Dt;
                return Rgb.Lerp(baseC, B(200, 210, 255), c.S[2] * MathF.Exp(-c.S[1] * 5f) * 0.8f);
            },
        },

        // ------------------------------------------------------------ ETKİLEŞİMLİ
        new()
        {
            Id = "yazma", Name = "Yazma Tepkisi", Category = CatReactive, Needs = Needs.Keys, Colors = 2,
            Description = "Tuşa bastıkça Renk 1 parlar; yazmayı bırakınca Renk 2'de loş bekler. (Yalnızca basış sayılır, hangi tuş olduğu kaydedilmez.)",
            Dot1 = B(255, 255, 255), Dot2 = B(0, 140, 255),
            Render = c =>
            {
                c.S[0] = Math.Min(1f, c.S[0] * MathF.Exp(-c.Dt * 2.5f) + c.NewKeys * 0.5f);
                return Rgb.Lerp(c.C2 * 0.3f, c.C1, c.S[0]);
            },
        },
        new()
        {
            Id = "yazmagokkusagi", Name = "Yazma Gökkuşağı", Category = CatReactive, Needs = Needs.Keys,
            Description = "Her tuş vuruşu rengi gökkuşağında bir adım ilerletir ve parlatır.",
            Dot1 = B(255, 0, 120), Dot2 = B(0, 255, 160),
            Render = c =>
            {
                if (c.S[2] == 0) { c.S[0] = c.Rand(); c.S[2] = 1; }
                if (c.NewKeys > 0) { c.S[0] = Frac(c.S[0] + 0.055f * c.NewKeys); c.S[1] = 1; }
                c.S[1] *= MathF.Exp(-c.Dt * 1.8f);
                return Rgb.Hsv(c.S[0], 1, 0.25f + 0.75f * c.S[1]);
            },
        },
        new()
        {
            Id = "yazmahizi", Name = "Yazma Hızı", Category = CatReactive, Needs = Needs.Keys,
            Description = "Ne kadar hızlı yazarsan o kadar ısınır: mavi (yavaş) → yeşil → sarı → kırmızı (çok hızlı).",
            Dot1 = B(0, 80, 255), Dot2 = B(255, 0, 0),
            Render = c =>
            {
                float inst = c.RealDt > 0 ? c.NewKeys / c.RealDt : 0;
                c.S[0] += (inst - c.S[0]) * (1 - MathF.Exp(-c.RealDt / 1.5f));
                float k = Math.Clamp(c.S[0] / 7f, 0f, 1f);
                return Rgb.Hsv(0.66f * (1 - k), 1, 0.3f + 0.7f * Math.Clamp(c.S[0] / 2f, 0f, 1f));
            },
        },
        new()
        {
            Id = "muzik", Name = "Müzik Ritmi", Category = CatReactive, Needs = Needs.Audio, Colors = 2,
            Description = "Bilgisayardan çıkan sesin ritmine göre parlar: vuruşlarda Renk 1, sessizlikte loş Renk 2. (Mikrofon kullanılmaz.)",
            Dot1 = B(255, 0, 200), Dot2 = B(40, 0, 90),
            Render = c => Rgb.Lerp(c.C2 * 0.08f, c.C1, c.Audio),
        },
        new()
        {
            Id = "muzikgokkusagi", Name = "Müzik Gökkuşağı", Category = CatReactive, Needs = Needs.Audio,
            Description = "Müzik yükseldikçe renkler hızla döner ve parlar, sessizlikte söner.",
            Dot1 = B(0, 255, 200), Dot2 = B(255, 0, 100),
            Render = c =>
            {
                c.S[0] = Frac(c.S[0] + c.Dt * (0.02f + 0.5f * c.Audio));
                return Rgb.Hsv(c.S[0], 1, 0.06f + 0.94f * c.Audio);
            },
        },
        new()
        {
            Id = "muzikisi", Name = "Müzik Isısı", Category = CatReactive, Needs = Needs.Audio,
            Description = "Sesin şiddetini renge çevirir: sakin = mor/mavi, güçlü vuruş = kırmızı/beyaz.",
            Dot1 = B(120, 0, 255), Dot2 = B(255, 40, 0),
            Render = c =>
            {
                var col = Rgb.Hsv(0.72f * (1 - c.Audio), 1, 0.12f + 0.88f * c.Audio);
                return c.Audio > 0.9f ? Rgb.Lerp(col, B(255, 230, 220), (c.Audio - 0.9f) * 6f) : col;
            },
        },

        new()
        {
            Id = "ekran", Name = "Ekran Rengi", Category = CatReactive, Needs = Needs.Screen, MaxFps = 12,
            Description = "Ekrandaki baskın rengi klavyeye yansıtır (ambilight gibi): film, oyun, duvar kağıdı ne açıksa ona uyar. Hız = tepki hızı.",
            Dot1 = B(255, 120, 0), Dot2 = B(0, 160, 255),
            Render = c => ScreenFollow(c, 4f, 1.0f),
        },
        new()
        {
            Id = "ekrancanli", Name = "Ekran Rengi · Canlı", Category = CatReactive, Needs = Needs.Screen, MaxFps = 15,
            Description = "Ekran örneklemenin oyun/video sürümü: daha hızlı tepki verir, renkleri daha canlı gösterir. Hız = tepki hızı.",
            Dot1 = B(255, 0, 90), Dot2 = B(0, 255, 140),
            Render = c => ScreenFollow(c, 12f, 1.35f),
        },

        new()
        {
            Id = "ekranalt", Name = "Ekran Alt Ambiyansı", Category = CatReactive, Needs = Needs.Screen, MaxFps = 12, ScreenPart = 0.25f,
            Description = "Sadece ekranın alt şeridini örnekler, en alt kenar daha çok etkiler: ekranın ışığı klavyeye taşıyor gibi. Hız = tepki hızı.",
            Dot1 = B(255, 150, 40), Dot2 = B(40, 20, 120),
            Render = c => ScreenFollow(c, 5f, 1.2f),
        },

        // ------------------------------------------------------------ SİSTEM
        new()
        {
            Id = "cpu", Name = "İşlemci Yükü", Category = CatSystem, Needs = Needs.Cpu, MaxFps = 10,
            Description = "İşlemci boştayken yeşil, yük arttıkça sarı → kırmızı; %85 üstünde nabız atar.",
            Dot1 = B(0, 255, 0), Dot2 = B(255, 0, 0),
            Render = c =>
            {
                c.S[0] += (c.Cpu - c.S[0]) * (1 - MathF.Exp(-c.RealDt * 3));
                float l = c.S[0];
                var col = Rgb.Hsv(0.33f * (1 - l), 1, 1);
                return l > 0.85f ? col * (0.55f + 0.45f * Wave(c.T, 0.6f)) : col;
            },
        },
        new()
        {
            Id = "pil", Name = "Pil Durumu", Category = CatSystem, Needs = Needs.Battery, MaxFps = 8,
            Description = "Pil doluyken yeşil, azaldıkça kırmızı. Şarj olurken yavaşça nefes alır, %15 altında yanıp söner.",
            Dot1 = B(0, 255, 60), Dot2 = B(255, 40, 0),
            Render = c =>
            {
                var col = Rgb.Hsv(0.33f * Math.Clamp((c.Battery - 0.1f) / 0.8f, 0f, 1f), 1, 1);
                if (c.Charging) return col * (0.35f + 0.65f * Wave(c.T, 3f));
                if (c.Battery < 0.15f && !c.OnAc) return (c.T % 1.2f) < 0.6f ? col : col * 0.15f;
                return col;
            },
        },
        new()
        {
            Id = "gundongusu", Name = "Gün Döngüsü", Category = CatSystem, Kind = Kind.Slow, SlowMs = 20000,
            Description = "Saate göre renk: sabah yumuşak turuncu, öğlen serin beyaz, akşam gün batımı, gece loş amber (göz yormaz).",
            Dot1 = B(255, 200, 150), Dot2 = B(255, 70, 10),
            Render = c =>
            {
                float hr = (float)c.Now.TimeOfDay.TotalHours;
                for (int i = 0; i < DayKeys.Length - 1; i++)
                {
                    var (h0, c0) = DayKeys[i];
                    var (h1, c1) = DayKeys[i + 1];
                    if (hr >= h0 && hr <= h1) return Rgb.Lerp(c0, c1, (hr - h0) / (h1 - h0));
                }
                return DayKeys[0].c;
            },
        },
    };

    /// <summary>Ekran rengine yumuşakça yaklaşır. rate: tepki hızı (1/sn), sat: doygunluk çarpanı.</summary>
    private static Rgb ScreenFollow(EffectContext c, float rate, float sat)
    {
        if (c.S[3] == 0 && c.ScreenOk) { c.S[0] = c.Screen.R; c.S[1] = c.Screen.G; c.S[2] = c.Screen.B; c.S[3] = 1; }
        if (!c.ScreenOk) return new Rgb(c.S[0], c.S[1], c.S[2]);
        var t = c.Screen;
        if (sat != 1f)
        {
            float m = (t.R + t.G + t.B) / 3f;
            t = new Rgb(m + (t.R - m) * sat, m + (t.G - m) * sat, m + (t.B - m) * sat);
        }
        float k = 1 - MathF.Exp(-c.Dt * rate);
        c.S[0] += (Math.Clamp(t.R, 0f, 1f) - c.S[0]) * k;
        c.S[1] += (Math.Clamp(t.G, 0f, 1f) - c.S[1]) * k;
        c.S[2] += (Math.Clamp(t.B, 0f, 1f) - c.S[2]) * k;
        return new Rgb(c.S[0], c.S[1], c.S[2]);
    }

    public static string CategoryTitle(string cat) => cat switch
    {
        CatBasic => "Temel",
        CatShow => "Renk Şovu",
        CatNature => "Doğa && Atmosfer",
        CatReactive => "Etkileşimli",
        CatSystem => "Sistem",
        _ => cat,
    };

    public static Effect ById(string? id) => All.FirstOrDefault(e => e.Id == id) ?? All[1];
}
