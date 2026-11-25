using System.Runtime.InteropServices;

namespace KlavyeIsik;

// ------------------------------------------------------------------ Ses seviyesi (Windows ses ölçeri, çok ucuz)

[ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
internal class MMDeviceEnumeratorCom { }

[ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceEnumerator
{
    [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
    [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
}

[ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDevice
{
    [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
}

[ComImport, Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioMeterInformation
{
    [PreserveSig] int GetPeakValue(out float peak);
}

/// <summary>Varsayılan hoparlör çıkışının anlık tepe seviyesini okur. Ses kaydı yapmaz.</summary>
internal sealed class AudioMeter : IDisposable
{
    private IAudioMeterInformation? _meter;
    private double _refreshAt;

    public float Peak(double now)
    {
        if (_meter == null || now >= _refreshAt)
        {
            Acquire();
            _refreshAt = now + 4; // varsayılan cihaz değişirse yakala
        }
        if (_meter == null) return 0;
        try
        {
            return _meter.GetPeakValue(out float p) == 0 ? p : 0;
        }
        catch
        {
            Release();
            return 0;
        }
    }

    private void Acquire()
    {
        Release();
        IMMDeviceEnumerator? en = null;
        IMMDevice? dev = null;
        try
        {
            en = (IMMDeviceEnumerator)new MMDeviceEnumeratorCom();
            if (en.GetDefaultAudioEndpoint(0 /*render*/, 1 /*multimedia*/, out dev) != 0 || dev == null) return;
            var iid = typeof(IAudioMeterInformation).GUID;
            if (dev.Activate(ref iid, 0x17, IntPtr.Zero, out object o) == 0)
                _meter = (IAudioMeterInformation)o;
        }
        catch
        {
            _meter = null;
        }
        finally
        {
            if (dev != null) Marshal.ReleaseComObject(dev);
            if (en != null) Marshal.ReleaseComObject(en);
        }
    }

    private void Release()
    {
        if (_meter != null)
        {
            try { Marshal.ReleaseComObject(_meter); } catch { }
            _meter = null;
        }
    }

    public void Dispose() => Release();
}

// ------------------------------------------------------------------ Ekran örnekleme

/// <summary>
/// Ekranı 64x36 piksellik minik bir görüntüye küçülterek kopyalar (ekran görüntüsü kaydedilmez, hiçbir yere yazılmaz)
/// ve baskın rengi bulur. Canlı (doygun) renkler gri/beyaz alanlardan daha ağır basar.
/// </summary>
internal sealed unsafe class ScreenSampler : IDisposable
{
    private const int W = 64, H = 36;
    private IntPtr _memDc, _bmp, _old, _bits;

    public ScreenSampler()
    {
        var screen = Native.GetDC(IntPtr.Zero);
        _memDc = Native.CreateCompatibleDC(screen);
        var bi = new Native.BITMAPINFOHEADER
        {
            biSize = sizeof(Native.BITMAPINFOHEADER), biWidth = W, biHeight = -H, biPlanes = 1, biBitCount = 32,
        };
        _bmp = Native.CreateDIBSection(screen, ref bi, 0, out _bits, IntPtr.Zero, 0);
        Native.ReleaseDC(IntPtr.Zero, screen);
        if (_memDc != IntPtr.Zero && _bmp != IntPtr.Zero)
        {
            _old = Native.SelectObject(_memDc, _bmp);
            Native.SetStretchBltMode(_memDc, 3 /*COLORONCOLOR: hızlı örnekleme*/);
        }
    }

    /// <param name="bottomFraction">1 = tüm ekran; 0.25 = sadece en alttaki %25'lik şerit (klavyeye en yakın kısım).</param>
    public bool Sample(out Rgb color, float bottomFraction = 1f)
    {
        color = Rgb.Black;
        if (_bits == IntPtr.Zero) return false;

        // Görev çubuğu hariç çalışma alanı (görev çubuğu hangi kenarda olursa olsun).
        Rectangle wa;
        try { wa = Screen.PrimaryScreen?.WorkingArea ?? Rectangle.Empty; } catch { wa = Rectangle.Empty; }
        if (wa.Width <= 0 || wa.Height <= 0)
            wa = new Rectangle(0, 0, Native.GetSystemMetrics(0), Native.GetSystemMetrics(1));
        if (wa.Width <= 0 || wa.Height <= 0) return false;

        bottomFraction = Math.Clamp(bottomFraction, 0.05f, 1f);
        int regionH = Math.Max(1, (int)(wa.Height * bottomFraction));
        int regionY = wa.Bottom - regionH;
        bool strip = bottomFraction < 0.999f;

        var screen = Native.GetDC(IntPtr.Zero);
        if (screen == IntPtr.Zero) return false;
        bool ok = Native.StretchBlt(_memDc, 0, 0, W, H, screen, wa.X, regionY, wa.Width, regionH, 0x00CC0020 /*SRCCOPY*/);
        Native.ReleaseDC(IntPtr.Zero, screen);
        if (!ok) return false;
        Native.GdiFlush();

        byte* p = (byte*)_bits;
        float sr = 0, sg = 0, sb = 0, sw8 = 0, swv = 0, sumV = 0, sumRow = 0;
        for (int y = 0; y < H; y++)
        {
            // Alt şerit modunda klavyeye (ekranın alt kenarına) yakın satırlar daha ağır basar.
            float rowW = strip ? 0.35f + 0.65f * y / (H - 1f) : 1f;
            for (int x = 0; x < W; x++, p += 4)
            {
                int b = p[0], g = p[1], r = p[2];
                int mx = Math.Max(r, Math.Max(g, b)), mn = Math.Min(r, Math.Min(g, b));
                float v = mx / 255f;
                float sat = mx > 0 ? (mx - mn) / (float)mx : 0;
                float w = (sat * sat * v + 0.015f) * rowW;
                sr += r * w; sg += g * w; sb += b * w;
                sw8 += w; swv += w * v; sumV += v * rowW; sumRow += rowW;
            }
        }

        float cr = sr / sw8, cg = sg / sw8, cb = sb / sw8;
        float m = Math.Max(cr, Math.Max(cg, cb));
        if (m < 1f) { color = Rgb.Black; return true; }
        var hue = new Rgb(cr / m, cg / m, cb / m);

        float avgV = sumV / sumRow, vivV = swv / sw8;
        float vOut = Math.Clamp(0.12f + 0.88f * Math.Max(avgV * 1.3f, vivV * 0.85f), 0.12f, 1f);
        color = hue * vOut;
        return true;
    }

    public void Dispose()
    {
        if (_memDc != IntPtr.Zero)
        {
            if (_old != IntPtr.Zero) Native.SelectObject(_memDc, _old);
            Native.DeleteDC(_memDc);
            _memDc = IntPtr.Zero;
        }
        if (_bmp != IntPtr.Zero) { Native.DeleteObject(_bmp); _bmp = IntPtr.Zero; }
        _bits = IntPtr.Zero;
    }
}

// ------------------------------------------------------------------ İşlemci yükü

internal sealed class CpuMeter
{
    private long _idle, _kernel, _user;
    private bool _has;
    public float Load { get; private set; }

    public void Sample()
    {
        if (!Native.GetSystemTimes(out long idle, out long kernel, out long user)) return;
        if (_has)
        {
            long di = idle - _idle, total = (kernel - _kernel) + (user - _user);
            if (total > 0) Load = Math.Clamp((float)(total - di) / total, 0f, 1f);
        }
        _idle = idle; _kernel = kernel; _user = user; _has = true;
    }
}

// ------------------------------------------------------------------ Klavye kancası

/// <summary>Sadece "bir tuşa basıldı" bilgisini verir; tuşun ne olduğu hiçbir yere kaydedilmez.</summary>
internal sealed class KeyboardHook : IDisposable
{
    private readonly Native.LowLevelKeyboardProc _proc;
    private readonly Action _onPress;
    private readonly bool[] _down = new bool[256];
    private IntPtr _hook;

    public KeyboardHook(Action onPress)
    {
        _onPress = onPress;
        _proc = Callback;
        _hook = Native.SetWindowsHookExW(13 /*WH_KEYBOARD_LL*/, _proc, Native.GetModuleHandleW(null), 0);
    }

    private IntPtr Callback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0)
        {
            int vk = Marshal.ReadInt32(lParam) & 0xFF;
            int msg = (int)wParam;
            if (msg is 0x100 or 0x104)
            {
                if (!_down[vk]) { _down[vk] = true; _onPress(); }
            }
            else if (msg is 0x101 or 0x105)
            {
                _down[vk] = false;
            }
        }
        return Native.CallNextHookEx(_hook, code, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
        {
            Native.UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
    }
}
