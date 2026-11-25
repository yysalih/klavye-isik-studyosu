using System.Diagnostics;

namespace KlavyeIsik;

[Flags]
public enum PauseReason { None = 0, Locked = 1, DisplayOff = 2, Suspended = 4 }

/// <summary>
/// Efektleri ayrı, düşük öncelikli bir iş parçacığında çalıştırır.
/// Performans kuralları:
///  - Renk değişmediyse klavyeye hiçbir şey gönderilmez (her gönderim çekirdekte ~4 ms sürer).
///  - Sabit efektlerde / ışık kapalıyken iş parçacığı tamamen uyur.
///  - Kare zamanlaması yüksek çözünürlüklü zamanlayıcı ile yapılır; sistem saat çözünürlüğü yükseltilmez.
///  - Eko modda iş parçacığı Windows "verimlilik modu"na (EcoQoS) alınır.
/// </summary>
public sealed class LightEngine : IDisposable
{
    public static readonly int[] PerfFps = { 12, 20, 30 };
    public static readonly string[] PerfNames = { "Eko", "Dengeli", "Akıcı" };

    private readonly ClevoKeyboard _kb;
    private readonly Thread _thread;
    private readonly AutoResetEvent _wake = new(false);
    private readonly TimerWaitHandle? _timer;
    private readonly WaitHandle[] _waitSet;
    private readonly EffectContext _ctx = new();
    private volatile bool _run = true;

    private volatile Effect _effect;
    private volatile int _c1 = 0x008CFF, _c2 = 0xFF0078;
    private volatile float _brightness = 0.85f, _speed = 1f;
    private volatile int _perf = 1;
    private volatile bool _lightsOn = true, _onBattery, _ecoOnBattery = true;
    private volatile bool _forceResend, _reopen;
    private int _pause;
    private int _keyCount;

    private volatile int _current;      // klavyeye en son giden renk (önizleme için)
    private long _sendTicks;
    private int _sendCount;

    public LightEngine(ClevoKeyboard kb, Effect initial)
    {
        _kb = kb;
        _effect = initial;
        _timer = TimerWaitHandle.Create();
        _waitSet = _timer != null ? new WaitHandle[] { _wake, _timer } : new WaitHandle[] { _wake };
        _thread = new Thread(Loop) { IsBackground = true, Name = "IsikMotoru", Priority = ThreadPriority.BelowNormal };
    }

    public void Start() => _thread.Start();

    // ------------------------------------------------------------ Ayarlar (UI iş parçacığından)

    public Effect Effect { get => _effect; set { _effect = value; Wake(); } }
    public Color Color1 { get => Rgb.Unpack(_c1); set { _c1 = Rgb.FromColor(value).Pack(); Wake(); } }
    public Color Color2 { get => Rgb.Unpack(_c2); set { _c2 = Rgb.FromColor(value).Pack(); Wake(); } }
    public int BrightnessPercent { set { _brightness = Math.Clamp(value, 0, 100) / 100f; Wake(); } }
    public int SpeedPercent { set { _speed = Math.Clamp(value, 10, 300) / 100f; Wake(); } }
    public int Performance { get => _perf; set { _perf = Math.Clamp(value, 0, 2); Wake(); } }
    public bool LightsOn { get => _lightsOn; set { _lightsOn = value; Wake(); } }
    public bool OnBattery { get => _onBattery; set { _onBattery = value; Wake(); } }
    public bool EcoOnBattery { set { _ecoOnBattery = value; Wake(); } }

    public bool IsEcoActive => _perf == 0 || (_ecoOnBattery && _onBattery);
    public int EffectiveFps => IsEcoActive ? PerfFps[0] : PerfFps[_perf];

    public void SetPause(PauseReason reason, bool on)
    {
        int old, upd;
        do
        {
            old = Volatile.Read(ref _pause);
            upd = on ? old | (int)reason : old & ~(int)reason;
        } while (Interlocked.CompareExchange(ref _pause, upd, old) != old);
        if (!on && reason == PauseReason.Suspended) { _reopen = true; _forceResend = true; }
        Wake();
    }

    public bool IsPaused => Volatile.Read(ref _pause) != 0;

    /// <summary>Oturum kilitliyken ışık yanmaya devam eder; sadece ekran örnekleme son rengi korur
    /// (kilit ekranında masaüstü görünmediği için örnekleme siyah döner).</summary>
    public volatile bool SessionLocked;

    /// <summary>Uyku sonrası vb. donanım rengini kaybetmiş olabilir: yeniden gönder.</summary>
    public void ForceRefresh() { _forceResend = true; Wake(); }

    public void NotifyKeyPress()
    {
        Interlocked.Increment(ref _keyCount);
        _wake.Set();
    }

    public Color CurrentColor => Rgb.Unpack(_current);
    public string DeviceStatus => _kb.Status;
    public ClevoKeyboard Keyboard => _kb;
    public bool DeviceOk { get; private set; }

    /// <summary>Son çağrıdan beri: gönderim sayısı ve sürücüde geçen toplam süre (ms).</summary>
    public (int sends, double busyMs) TakeStats()
    {
        int n = Interlocked.Exchange(ref _sendCount, 0);
        long t = Interlocked.Exchange(ref _sendTicks, 0);
        return (n, t * 1000.0 / Stopwatch.Frequency);
    }

    private void Wake() => _wake.Set();

    // ------------------------------------------------------------ Motor döngüsü

    private void Loop()
    {
        var clock = Stopwatch.StartNew();
        double last = 0, phase = 0, nextReopen = 0, nextCpu = 0, nextBattery = 0;
        int lastSent = -1, lastKeys = Volatile.Read(ref _keyCount), stableFrames = 0;
        Effect? active = null;
        bool ecoApplied = false;
        AudioMeter? audio = null;
        ScreenSampler? screen = null;
        var cpu = new CpuMeter();
        float audioAvg = 0, audioMax = 0.03f, env = 0;

        DeviceOk = _kb.Open();

        while (_run)
        {
            double now = clock.Elapsed.TotalSeconds;
            float realDt = (float)Math.Min(now - last, 0.25);
            last = now;

            var eff = _effect;
            if (!ReferenceEquals(eff, active))
            {
                active = eff;
                _ctx.Reset();
                phase = 0;
                lastKeys = Volatile.Read(ref _keyCount);
                stableFrames = 0;
                if ((eff.Needs & Needs.Audio) == 0 && audio != null) { audio.Dispose(); audio = null; }
                if ((eff.Needs & Needs.Screen) == 0 && screen != null) { screen.Dispose(); screen = null; }
            }

            bool eco = IsEcoActive;
            if (eco != ecoApplied) { ApplyEcoQoS(eco); ecoApplied = eco; }

            if (_reopen || (!DeviceOk && now >= nextReopen))
            {
                _reopen = false;
                DeviceOk = _kb.Open();
                // Desteklenmeyen klavye / yönetici izni eksik: sık sık yeniden denemenin anlamı yok.
                nextReopen = now + (_kb.Unsupported || _kb.NeedsAdmin ? 60 : 3);
                lastSent = -1;
            }
            if (_forceResend) { _forceResend = false; lastSent = -1; }

            bool lit = _lightsOn && Volatile.Read(ref _pause) == 0;
            int packed = 0;

            if (lit)
            {
                float speed = _speed;
                phase += realDt * speed;
                _ctx.T = (float)(phase % 100000.0);
                _ctx.Dt = realDt * speed;
                _ctx.RealDt = realDt;
                _ctx.C1 = Rgb.Bytes((_c1 >> 16) & 255, (_c1 >> 8) & 255, _c1 & 255);
                _ctx.C2 = Rgb.Bytes((_c2 >> 16) & 255, (_c2 >> 8) & 255, _c2 & 255);
                _ctx.Now = DateTime.Now;

                int keys = Volatile.Read(ref _keyCount);
                _ctx.NewKeys = keys - lastKeys;
                lastKeys = keys;

                if ((eff.Needs & Needs.Audio) != 0)
                {
                    audio ??= new AudioMeter();
                    float peak = audio.Peak(now);
                    audioAvg += (peak - audioAvg) * (1 - MathF.Exp(-realDt / 1.2f));
                    audioMax = Math.Max(peak, Math.Max(0.03f, audioMax * MathF.Exp(-realDt / 4f)));
                    float lo = audioAvg * 0.55f;
                    float n = Math.Clamp((peak - lo) / Math.Max(audioMax - lo, 0.02f), 0f, 1f);
                    env = n > env ? n : env * MathF.Exp(-realDt * 7f);
                    _ctx.Audio = env;
                }
                if ((eff.Needs & Needs.Screen) != 0)
                {
                    screen ??= new ScreenSampler();
                    if (SessionLocked) _ctx.ScreenOk = false; // son rengi koru
                    else
                    {
                        _ctx.ScreenOk = screen.Sample(out var sc, eff.ScreenPart);
                        if (_ctx.ScreenOk) _ctx.Screen = sc;
                    }
                }
                if ((eff.Needs & Needs.Cpu) != 0 && now >= nextCpu)
                {
                    cpu.Sample();
                    _ctx.Cpu = cpu.Load;
                    nextCpu = now + 0.5;
                }
                if ((eff.Needs & Needs.Battery) != 0 && now >= nextBattery)
                {
                    var ps = SystemInformation.PowerStatus;
                    float pct = ps.BatteryLifePercent;
                    _ctx.Battery = pct is >= 0 and <= 1 ? pct : 1f;
                    _ctx.Charging = (ps.BatteryChargeStatus & BatteryChargeStatus.Charging) != 0;
                    _ctx.OnAc = ps.PowerLineStatus != PowerLineStatus.Offline;
                    nextBattery = now + 5;
                }

                Rgb c;
                try { c = eff.Render(_ctx); }
                catch { c = _ctx.C1; }

                float b = _brightness;
                float scale = b <= 0 ? 0 : MathF.Pow(b, 1.6f);
                packed = (c * scale).Pack();
            }

            // Sadece görünür bir değişiklik varsa gönder.
            int threshold = eco ? 3 : 1;
            if (lastSent < 0 || ChannelDiff(packed, lastSent) >= threshold || (packed == 0 && lastSent != 0))
            {
                if (DeviceOk)
                {
                    long t0 = Stopwatch.GetTimestamp();
                    bool ok = _kb.SetColor((packed >> 16) & 255, (packed >> 8) & 255, packed & 255);
                    Interlocked.Add(ref _sendTicks, Stopwatch.GetTimestamp() - t0);
                    Interlocked.Increment(ref _sendCount);
                    if (ok) lastSent = packed;
                    else { DeviceOk = false; nextReopen = now + 1; }
                }
                stableFrames = 0;
            }
            else
            {
                stableFrames++;
            }
            _current = packed;

            // ---- Bekleme
            if (!lit || eff.Kind == Kind.Static)
            {
                _wake.WaitOne(DeviceOk ? Timeout.Infinite : 1000);
                continue;
            }
            if (eff.Kind == Kind.Slow)
            {
                _wake.WaitOne(eff.SlowMs);
                continue;
            }

            int fps = EffectiveFps;
            if (eff.MaxFps > 0) fps = Math.Min(fps, eff.MaxFps);
            if (_kb.MaxFps > 0) fps = Math.Min(fps, _kb.MaxFps); // eski WMI yolu yavaş

            // Tuş tepkili efektlerde, renk oturduktan sonra bir sonraki tuşa kadar uyu.
            if ((eff.Needs & Needs.Keys) != 0 && stableFrames > fps)
            {
                _wake.WaitOne(1000);
                last = clock.Elapsed.TotalSeconds - 1.0 / fps;
                continue;
            }

            int frameMs = 1000 / fps;
            int spent = (int)((clock.Elapsed.TotalSeconds - now) * 1000);
            int waitMs = Math.Max(1, frameMs - spent);
            if (_timer != null)
            {
                _timer.Arm(waitMs);
                WaitHandle.WaitAny(_waitSet);
            }
            else
            {
                _wake.WaitOne(waitMs);
            }
        }

        audio?.Dispose();
        screen?.Dispose();
    }

    private static int ChannelDiff(int a, int b)
    {
        int dr = Math.Abs(((a >> 16) & 255) - ((b >> 16) & 255));
        int dg = Math.Abs(((a >> 8) & 255) - ((b >> 8) & 255));
        int db = Math.Abs((a & 255) - (b & 255));
        return Math.Max(dr, Math.Max(dg, db));
    }

    private static void ApplyEcoQoS(bool on)
    {
        try
        {
            var st = new Native.THREAD_POWER_THROTTLING_STATE { Version = 1, ControlMask = 1, StateMask = on ? 1u : 0u };
            Native.SetThreadInformation(Native.GetCurrentThread(), 3 /*ThreadPowerThrottling*/, ref st,
                System.Runtime.InteropServices.Marshal.SizeOf<Native.THREAD_POWER_THROTTLING_STATE>());
        }
        catch { /* eski Windows: yok say */ }
    }

    /// <summary>Motoru durdurur ve klavyeyi sabit bir son renge bırakır.</summary>
    public void Stop(Color? finalColor)
    {
        _run = false;
        _wake.Set();
        _thread.Join(1500);
        if (finalColor is { } fc)
        {
            if (!_kb.IsOpen) _kb.Open();
            _kb.SetColor(fc.R, fc.G, fc.B);
        }
    }

    public void Dispose()
    {
        _wake.Dispose();
        _timer?.Dispose();
    }
}
