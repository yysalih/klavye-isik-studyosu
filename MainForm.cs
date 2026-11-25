using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace KlavyeIsik;

public sealed class MainForm : Form
{
    private static readonly (string name, Color c)[] Presets =
    {
        ("Kırmızı", Color.FromArgb(255, 0, 0)), ("Turuncu", Color.FromArgb(255, 90, 0)), ("Amber", Color.FromArgb(255, 170, 0)),
        ("Sarı", Color.FromArgb(255, 240, 0)), ("Limon", Color.FromArgb(140, 255, 0)), ("Yeşil", Color.FromArgb(0, 255, 40)),
        ("Turkuaz", Color.FromArgb(0, 255, 170)), ("Camgöbeği", Color.FromArgb(0, 220, 255)), ("Mavi", Color.FromArgb(0, 90, 255)),
        ("İndigo", Color.FromArgb(60, 0, 255)), ("Mor", Color.FromArgb(150, 0, 255)), ("Pembe", Color.FromArgb(255, 0, 170)),
        ("Sıcak beyaz", Color.FromArgb(255, 180, 100)), ("Beyaz", Color.FromArgb(255, 255, 255)),
    };

    private readonly AppSettings _s;
    private readonly ClevoKeyboard _kb = new();
    private readonly LightEngine _engine;
    private readonly bool _startHidden;
    private bool _shownOnce, _allowClose, _loading = true;

    private readonly float _k;
    private readonly EffectList _list = new();
    private readonly KeyboardPreview _preview = new();
    private readonly Label _title = new(), _sub = new(), _effName = new(), _effDesc = new(), _stats = new(), _footer = new();
    private readonly Label _brightVal = new(), _speedVal = new(), _swatchHint = new();
    private readonly ColorChip _chip1 = new() { Caption = "Renk 1" }, _chip2 = new() { Caption = "Renk 2" };
    private readonly List<Swatch> _swatches = new();
    private readonly DarkSlider _bright = new(), _speed = new();
    private readonly Segmented _perf = new();
    private readonly Toggle _tAuto = new(), _tTray = new(), _tEco = new();
    private readonly Segmented _idle = new();
    private readonly Label _idleLbl = new();
    private readonly System.Windows.Forms.Timer _idleTimer = new();
    private static readonly string[] IdleNames = { "Hemen", "1 dk", "5 dk", "Asla" };
    private static readonly int[] IdleDelayMs = { 0, 60_000, 300_000, -1 };
    private readonly PillButton _power = new();
    private readonly NotifyIcon _tray = new();
    private readonly ContextMenuStrip _trayMenu = new();
    private ToolStripMenuItem? _trayPowerItem;
    private readonly Dictionary<string, ToolStripMenuItem> _trayEffectItems = new();
    private readonly System.Windows.Forms.Timer _uiTimer = new() { Interval = 66 };
    private readonly System.Windows.Forms.Timer _saveTimer = new() { Interval = 800 };
    private readonly Icon _icon;
    private KeyboardHook? _hook;
    private EventWaitHandle? _showEvent;
    private RegisteredWaitHandle? _showReg;
    private IntPtr _displayNotify;
    private readonly Process _proc = Process.GetCurrentProcess();
    private TimeSpan _lastCpu;
    private long _lastStatTick;
    private int _statCounter;

    private const int HkToggle = 1, HkNext = 2, HkPrev = 3, HkUp = 4, HkDown = 5;

    public MainForm(bool startHidden)
    {
        _startHidden = startHidden;
        _s = AppSettings.Load();
        _k = DeviceDpi / 96f;
        _icon = AppIcon.Create(32);

        var effect = EffectCatalog.ById(_s.Effect);
        _engine = new LightEngine(_kb, effect);

        Text = "Klavye Işık Stüdyosu";
        Icon = _icon;
        BackColor = Theme.Bg;
        ForeColor = Theme.Text;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        AutoScaleMode = AutoScaleMode.None;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(S(960), S(650));
        DoubleBuffered = true;

        BuildUi();
        BuildTray();
        ApplySettingsToUi(effect);

        _engine.OnBattery = SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Offline;
        _engine.Start();

        _uiTimer.Tick += (_, _) => UiTick();
        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); _s.Save(); };

        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.SessionSwitch += OnSessionSwitch;

        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, Program.ShowEventName);
        _showReg = ThreadPool.RegisterWaitForSingleObject(_showEvent, (_, _) =>
        {
            try { BeginInvoke(ShowFromTray); } catch { }
        }, null, Timeout.Infinite, false);

        if (startHidden)
        {
            var trim = new System.Windows.Forms.Timer { Interval = 3000 };
            trim.Tick += (_, _) => { trim.Stop(); trim.Dispose(); if (!Visible) TrimMemory(); };
            trim.Start();
        }

        _loading = false;
    }

    private int S(float v) => (int)MathF.Round(v * _k);

    // ================================================================== Arayüz

    private void BuildUi()
    {
        SuspendLayout();
        var titleFont = new Font("Segoe UI Semibold", 15f);
        var subFont = new Font("Segoe UI", 8.75f);
        var effFont = new Font("Segoe UI Semibold", 13f);
        var labelFont = new Font("Segoe UI Semibold", 9.25f);

        Lbl(_title, "Klavye Işık Stüdyosu", titleFont, Theme.Text, 20, 12, 520, 30);
        Lbl(_sub, "", subFont, Theme.Sub, 22, 42, 740, 20);
        _sub.Click += (_, _) => RelaunchAsAdmin();

        _power.SetBounds(S(960 - 20 - 150), S(16), S(150), S(36));
        _power.Font = new Font("Segoe UI Semibold", 9.5f);
        _power.Click += (_, _) => SetLights(!_engine.LightsOn);
        Controls.Add(_power);

        var listHost = new Panel { BackColor = Theme.Panel };
        listHost.SetBounds(S(16), S(72), S(272), S(560));
        listHost.Paint += (_, e) => { };
        _list.SetBounds(0, S(6), S(272), S(548));
        string? lastCat = null;
        foreach (var eff in EffectCatalog.All)
        {
            if (eff.Category != lastCat) { _list.Items.Add(eff.Category); lastCat = eff.Category; }
            _list.Items.Add(eff);
        }
        _list.SelectedIndexChanged += (_, _) => { if (_list.SelectedItem is Effect eff) SelectEffect(eff, fromList: true); };
        listHost.Controls.Add(_list);
        Controls.Add(listHost);

        const int X = 304, W = 640;
        _preview.SetBounds(S(X), S(72), S(W), S(228));
        Controls.Add(_preview);

        Lbl(_effName, "", effFont, Theme.Text, X, 310, W, 28);
        Lbl(_effDesc, "", subFont, Theme.Sub, X + 1, 338, W, 36);

        // Renkler
        _chip1.SetBounds(S(X), S(384), S(124), S(46));
        _chip2.SetBounds(S(X + 132), S(384), S(124), S(46));
        _chip1.Font = _chip2.Font = new Font("Segoe UI Semibold", 9f);
        _chip1.Click += (_, _) => PickColor(1);
        _chip2.Click += (_, _) => PickColor(2);
        Controls.Add(_chip1);
        Controls.Add(_chip2);

        int sx = X + 272;
        var tip = new ToolTip { InitialDelay = 300 };
        for (int i = 0; i < Presets.Length; i++)
        {
            var (name, c) = Presets[i];
            var sw = new Swatch { Color = c, Tip = name };
            sw.SetBounds(S(sx + (i % 7) * 26), S(384 + (i / 7) * 25), S(24), S(23));
            tip.SetToolTip(sw, $"{name} — sol tık: Renk 1, sağ tık: Renk 2");
            sw.MouseUp += (_, e) =>
            {
                if (!sw.Enabled) return;
                if (e.Button == MouseButtons.Right && _engine.Effect.Colors >= 2) SetColor(2, sw.Color);
                else SetColor(1, sw.Color);
            };
            _swatches.Add(sw);
            Controls.Add(sw);
        }
        Lbl(_swatchHint, "Hazır renkler\nSol tık: Renk 1\nSağ tık: Renk 2", subFont, Theme.Sub, sx + 7 * 26 + 8, 382, 140, 50);

        // Kaydırıcılar
        var lb1 = new Label(); Lbl(lb1, "Parlaklık", labelFont, Theme.Text, X, 446, 100, 26, ContentAlignment.MiddleLeft);
        _bright.SetBounds(S(X + 100), S(446), S(W - 100 - 64), S(26));
        _bright.Minimum = 0; _bright.Maximum = 100; _bright.SmallStep = 5;
        _bright.ValueChanged += (_, _) => { _s.Brightness = _bright.Value; _engine.BrightnessPercent = _bright.Value; _brightVal.Text = $"%{_bright.Value}"; Changed(); };
        Controls.Add(_bright);
        Lbl(_brightVal, "", labelFont, Theme.Sub, X + W - 60, 446, 60, 26, ContentAlignment.MiddleRight);

        var lb2 = new Label(); Lbl(lb2, "Hız", labelFont, Theme.Text, X, 482, 100, 26, ContentAlignment.MiddleLeft);
        _speed.SetBounds(S(X + 100), S(482), S(W - 100 - 64), S(26));
        _speed.Minimum = 10; _speed.Maximum = 300; _speed.SmallStep = 10;
        _speed.ValueChanged += (_, _) => { _s.Speed = _speed.Value; _engine.SpeedPercent = _speed.Value; _speedVal.Text = $"%{_speed.Value}"; Changed(); };
        Controls.Add(_speed);
        Lbl(_speedVal, "", labelFont, Theme.Sub, X + W - 60, 482, 60, 26, ContentAlignment.MiddleRight);

        // Performans
        var lb3 = new Label(); Lbl(lb3, "Performans", labelFont, Theme.Text, X, 522, 100, 32, ContentAlignment.MiddleLeft);
        _perf.Items = LightEngine.PerfNames.Select((n, i) => $"{n} · {LightEngine.PerfFps[i]} fps").ToArray();
        _perf.Font = new Font("Segoe UI", 9f);
        _perf.SetBounds(S(X + 100), S(522), S(300), S(32));
        _perf.SelectedIndexChanged += (_, _) => { _s.Performance = _perf.SelectedIndex; _engine.Performance = _perf.SelectedIndex; Changed(); };
        Controls.Add(_perf);
        Lbl(_stats, "", subFont, Theme.Sub, X + 412, 518, W - 412, 40);

        // Seçenekler
        Tgl(_tAuto, "Windows açılınca başlat", X, 568, 316);
        Tgl(_tTray, "Kapatınca simge durumunda çalış", X + 324, 568, 316);
        Tgl(_tEco, "Pildeyken otomatik Eko mod", X, 598, 316);
        Lbl(_idleLbl, "Kilitte ışığı kapat", new Font("Segoe UI", 9f), Theme.Text, X + 370, 596, 96, 26, ContentAlignment.MiddleLeft);
        _idle.Items = IdleNames;
        _idle.Font = new Font("Segoe UI", 8.5f);
        _idle.SetBounds(S(X + 468), S(595), S(172), S(28));
        tip.SetToolTip(_idle, "Ekran kilitlenince ya da kendiliğinden kapanınca, seçilen süre sonunda klavye ışığı söner.\nKilidi açınca kaldığı yerden yanar.");
        tip.SetToolTip(_idleLbl, "Ekran kilitlenince ya da kendiliğinden kapanınca, seçilen süre sonunda klavye ışığı söner.\nKilidi açınca kaldığı yerden yanar.");
        Controls.Add(_idle);
        _idleTimer.Tick += (_, _) => { _idleTimer.Stop(); _engine.SetPause(PauseReason.Locked, true); };
        _tAuto.CheckedChanged += (_, _) => { if (!_loading) Autostart.Set(_tAuto.Checked, elevated: _engine.Keyboard.Backend == KbBackend.Wmi); };
        _tTray.CheckedChanged += (_, _) => { _s.MinimizeToTray = _tTray.Checked; Changed(); };
        _tEco.CheckedChanged += (_, _) => { _s.EcoOnBattery = _tEco.Checked; _engine.EcoOnBattery = _tEco.Checked; Changed(); };
        _idle.SelectedIndexChanged += (_, _) =>
        {
            _s.IdleOff = _idle.SelectedIndex;
            UpdateIdlePause(restart: true);
            Changed();
        };

        Lbl(_footer, "Kısayollar:  Ctrl+Alt+K ışık aç/kapat   ·   Ctrl+Alt+PgDn / PgUp efekt değiştir   ·   Ctrl+Alt+Home / End parlaklık",
            subFont, Theme.Sub, 304, 628, 640, 18);

        ResumeLayout();
    }

    private void Lbl(Label l, string text, Font f, Color c, int x, int y, int w, int h, ContentAlignment align = ContentAlignment.TopLeft)
    {
        l.Text = text;
        l.Font = f;
        l.ForeColor = c;
        l.BackColor = Color.Transparent;
        l.TextAlign = align;
        l.AutoSize = false;
        l.SetBounds(S(x), S(y), S(w), S(h));
        Controls.Add(l);
    }

    private void Tgl(Toggle t, string text, int x, int y, int w)
    {
        t.Text = text;
        t.Font = new Font("Segoe UI", 9f);
        t.SetBounds(S(x), S(y), S(w), S(24));
        Controls.Add(t);
    }

    private void ApplySettingsToUi(Effect effect)
    {
        var c1 = AppSettings.ParseColor(_s.Color1, Color.FromArgb(0, 140, 255));
        var c2 = AppSettings.ParseColor(_s.Color2, Color.FromArgb(255, 0, 120));
        _engine.Color1 = c1;
        _engine.Color2 = c2;
        _chip1.Color = c1;
        _chip2.Color = c2;

        _bright.Value = _s.Brightness;
        _engine.BrightnessPercent = _s.Brightness;
        _brightVal.Text = $"%{_s.Brightness}";
        _speed.Value = _s.Speed;
        _engine.SpeedPercent = _s.Speed;
        _speedVal.Text = $"%{_s.Speed}";
        _perf.SelectedIndex = Math.Clamp(_s.Performance, 0, 2);
        _engine.Performance = _perf.SelectedIndex;

        _tAuto.Checked = Autostart.IsEnabled;
        _tTray.Checked = _s.MinimizeToTray;
        _tEco.Checked = _s.EcoOnBattery;
        _engine.EcoOnBattery = _s.EcoOnBattery;
        _idle.SelectedIndex = Math.Clamp(_s.IdleOff, 0, IdleNames.Length - 1);

        _engine.LightsOn = _s.LightsOn;
        UpdatePowerUi();
        SelectEffect(effect, fromList: false);
    }

    // ================================================================== Efekt / renk / ışık

    private void SelectEffect(Effect eff, bool fromList)
    {
        _engine.Effect = eff;
        _s.Effect = eff.Id;
        if (!fromList)
        {
            int idx = _list.Items.IndexOf(eff);
            if (idx >= 0 && _list.SelectedIndex != idx) _list.SelectedIndex = idx;
        }

        _effName.Text = eff.Name;
        string colorInfo = eff.Colors switch
        {
            0 => "Bu efekt kendi renklerini kullanır.",
            1 => "Renk 1'i kullanır.",
            _ => "Renk 1 ve Renk 2'yi kullanır.",
        };
        _effDesc.Text = eff.Description + "  " + colorInfo;

        _chip1.Enabled = eff.Colors >= 1;
        _chip2.Enabled = eff.Colors >= 2;
        foreach (var sw in _swatches) { sw.Enabled = eff.Colors >= 1; sw.Invalidate(); }
        _speed.Enabled = eff.Kind == Kind.Animated;

        foreach (var (id, item) in _trayEffectItems) item.Checked = id == eff.Id;
        UpdateHook();
        UpdateTrayText();
        Changed();
    }

    private void StepEffect(int dir)
    {
        var all = EffectCatalog.All;
        int i = Array.IndexOf(all, _engine.Effect);
        i = ((i + dir) % all.Length + all.Length) % all.Length;
        SelectEffect(all[i], fromList: false);
    }

    private void SetColor(int which, Color c)
    {
        if (which == 1) { _engine.Color1 = c; _chip1.Color = c; _s.Color1 = AppSettings.ToHex(c); }
        else { _engine.Color2 = c; _chip2.Color = c; _s.Color2 = AppSettings.ToHex(c); }
        Changed();
    }

    private void PickColor(int which)
    {
        if (which == 2 && _engine.Effect.Colors < 2) return;
        if (which == 1 && _engine.Effect.Colors < 1) return;
        using var dlg = new ColorDialog
        {
            FullOpen = true,
            AnyColor = true,
            Color = which == 1 ? _chip1.Color : _chip2.Color,
            CustomColors = Presets.Select(p => p.c.R | (p.c.G << 8) | (p.c.B << 16)).Take(16).ToArray(),
        };
        if (dlg.ShowDialog(this) == DialogResult.OK) SetColor(which, dlg.Color);
    }

    private void SetLights(bool on)
    {
        _engine.LightsOn = on;
        _s.LightsOn = on;
        UpdatePowerUi();
        Changed();
    }

    private void UpdatePowerUi()
    {
        bool on = _engine.LightsOn;
        _power.Active = on;
        _power.Text = on ? "Işık Açık" : "Işık Kapalı";
        if (_trayPowerItem != null) _trayPowerItem.Text = on ? "Işığı kapat" : "Işığı aç";
        UpdateTrayText();
    }

    private void SetBrightness(int v)
    {
        _bright.Value = Math.Clamp(v, 0, 100); // ValueChanged -> motor + kayıt
    }

    private void Changed()
    {
        if (_loading) return;
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void UpdateHook()
    {
        bool need = (_engine.Effect.Needs & Needs.Keys) != 0;
        if (need && _hook == null) _hook = new KeyboardHook(_engine.NotifyKeyPress);
        else if (!need && _hook != null) { _hook.Dispose(); _hook = null; }
    }

    // ================================================================== Arayüz zamanlayıcısı (sadece pencere görünürken)

    private void UiTick()
    {
        _preview.Color = _engine.CurrentColor;

        if (++_statCounter < 15) return; // ~1 sn
        _statCounter = 0;
        long nowTick = Stopwatch.GetTimestamp();
        double secs = _lastStatTick == 0 ? 1 : (nowTick - _lastStatTick) / (double)Stopwatch.Frequency;
        _lastStatTick = nowTick;

        var (sends, _) = _engine.TakeStats();
        _proc.Refresh();
        var cpu = _proc.TotalProcessorTime;
        double cpuPct = _lastCpu == TimeSpan.Zero ? 0 : (cpu - _lastCpu).TotalMilliseconds / (secs * 10.0 * Environment.ProcessorCount);
        _lastCpu = cpu;

        string mode = _engine.IsEcoActive && _engine.Performance != 0 ? "  (pil: Eko)" : "";
        string state = !_engine.DeviceOk ? "Klavye bağlantısı yok!" :
            _engine.IsPaused ? "Duraklatıldı (kilit/ekran kapalı)" :
            $"Klavyeye {sends / secs:0} güncelleme/sn{mode}";
        _stats.Text = $"{state}\nUygulama işlemci kullanımı: %{cpuPct:0.0}";
        UpdateDeviceLine();
    }

    private void UpdateDeviceLine()
    {
        var kb = _engine.Keyboard;
        bool admin = !_engine.DeviceOk && kb.NeedsAdmin;
        string text = _engine.DeviceOk
            ? $"{MachineName()} · {kb.TypeText} · {kb.Status}"
            : $"{MachineName()} · {kb.Status}" + (admin ? "  —  yönetici olarak açmak için tıkla" : "");
        if (_sub.Text != text) _sub.Text = text;
        _sub.ForeColor = admin ? Theme.Accent : (_engine.DeviceOk ? Theme.Sub : Color.FromArgb(255, 120, 120));
        _sub.Cursor = admin ? Cursors.Hand : Cursors.Default;
    }

    /// <summary>Eski (WMI) modeller yönetici izni ister: programı yönetici olarak yeniden başlatır.</summary>
    private void RelaunchAsAdmin()
    {
        if (!_engine.Keyboard.NeedsAdmin) return;
        try
        {
            Process.Start(new ProcessStartInfo(Environment.ProcessPath ?? Application.ExecutablePath, "--elevated")
            {
                UseShellExecute = true,
                Verb = "runas",
            });
            ExitApp();
        }
        catch { /* kullanıcı UAC penceresinde "Hayır" dedi */ }
    }

    private static string? _machine;

    /// <summary>Bilgisayarın modeli (BIOS kaydından), örn. "Monster Abra A5 V16.7".</summary>
    private static string MachineName()
    {
        if (_machine != null) return _machine;
        string name = "Clevo tabanlı dizüstü";
        try
        {
            using var k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\BIOS");
            var man = (k?.GetValue("SystemManufacturer") as string)?.Trim() ?? "";
            var prod = (k?.GetValue("SystemProductName") as string)?.Trim() ?? "";
            if (prod.Length > 0 && !prod.Contains("To be filled", StringComparison.OrdinalIgnoreCase))
            {
                var full = man.Length == 0 || prod.StartsWith(man, StringComparison.OrdinalIgnoreCase) ? prod : man + " " + prod;
                name = System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(full.ToLowerInvariant());
            }
        }
        catch { }
        return _machine = name;
    }

    // ================================================================== Tepsi

    private void BuildTray()
    {
        _trayMenu.Renderer = new DarkMenuRenderer();
        _trayMenu.ShowImageMargin = true;
        _trayMenu.Items.Add("Paneli aç", null, (_, _) => ShowFromTray()).Font = new Font("Segoe UI Semibold", 9f);
        _trayPowerItem = new ToolStripMenuItem("Işığı kapat", null, (_, _) => SetLights(!_engine.LightsOn));
        _trayMenu.Items.Add(_trayPowerItem);
        _trayMenu.Items.Add(new ToolStripSeparator());

        var effects = new ToolStripMenuItem("Efektler");
        ToolStripMenuItem? cat = null;
        string? lastCat = null;
        foreach (var eff in EffectCatalog.All)
        {
            if (eff.Category != lastCat)
            {
                lastCat = eff.Category;
                cat = new ToolStripMenuItem(EffectCatalog.CategoryTitle(eff.Category));
                effects.DropDownItems.Add(cat);
            }
            var e2 = eff;
            var item = new ToolStripMenuItem(eff.Name, null, (_, _) => SelectEffect(e2, fromList: false));
            _trayEffectItems[eff.Id] = item;
            cat!.DropDownItems.Add(item);
        }
        _trayMenu.Items.Add(effects);

        var bright = new ToolStripMenuItem("Parlaklık");
        foreach (var v in new[] { 100, 75, 50, 25, 10 })
            bright.DropDownItems.Add($"%{v}", null, (_, _) => SetBrightness(v));
        _trayMenu.Items.Add(bright);

        var perf = new ToolStripMenuItem("Performans");
        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            perf.DropDownItems.Add(_perf.Items[i], null, (_, _) => _perf.SelectedIndex = idx);
        }
        _trayMenu.Items.Add(perf);

        _trayMenu.Items.Add(new ToolStripSeparator());
        _trayMenu.Items.Add("Çıkış", null, (_, _) => ExitApp());
        foreach (ToolStripItem it in _trayMenu.Items) it.ForeColor = Theme.Text;

        _tray.Icon = _icon;
        _tray.ContextMenuStrip = _trayMenu;
        _tray.Visible = true;
        _tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) ShowFromTray(); };
    }

    private void UpdateTrayText()
    {
        var t = $"Klavye Işık – {_engine.Effect.Name}{(_engine.LightsOn ? "" : " (kapalı)")}";
        _tray.Text = t.Length > 63 ? t[..63] : t;
    }

    private void ShowFromTray()
    {
        _shownOnce = true;
        Show();
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Activate();
        BringToFront();
    }

    // ================================================================== Pencere yaşam döngüsü

    protected override void SetVisibleCore(bool value)
    {
        if (!_shownOnce && _startHidden)
        {
            _shownOnce = true;
            if (!IsHandleCreated) CreateHandle();
            value = false;
        }
        base.SetVisibleCore(value);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int dark = 1;
        Native.DwmSetWindowAttribute(Handle, 20, ref dark, 4);

        const uint MOD = 0x0001 /*Alt*/ | 0x0002 /*Ctrl*/ | 0x4000 /*NoRepeat*/;
        Native.RegisterHotKey(Handle, HkToggle, MOD, (uint)Keys.K);
        Native.RegisterHotKey(Handle, HkNext, MOD, (uint)Keys.PageDown);
        Native.RegisterHotKey(Handle, HkPrev, MOD, (uint)Keys.PageUp);
        Native.RegisterHotKey(Handle, HkUp, 0x0001 | 0x0002, (uint)Keys.Home);
        Native.RegisterHotKey(Handle, HkDown, 0x0001 | 0x0002, (uint)Keys.End);

        var guid = new Guid("6fe69556-704a-47a0-8f24-c28d936fda47"); // GUID_CONSOLE_DISPLAY_STATE
        _displayNotify = Native.RegisterPowerSettingNotification(Handle, ref guid, 0);
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        UpdateUiTimer();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateUiTimer();
    }

    private void UpdateUiTimer()
    {
        bool run = Visible && WindowState != FormWindowState.Minimized;
        if (run && !_uiTimer.Enabled) { _lastStatTick = 0; _lastCpu = TimeSpan.Zero; _statCounter = 14; _uiTimer.Start(); }
        else if (!run && _uiTimer.Enabled) _uiTimer.Stop();
        if (!run && IsHandleCreated) BeginInvoke(TrimMemory);
    }

    /// <summary>Tepsideyken kullanılmayan belleği Windows'a geri ver.</summary>
    private void TrimMemory()
    {
        try
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Native.EmptyWorkingSet(_proc.Handle);
        }
        catch { }
    }

    protected override void WndProc(ref Message m)
    {
        const int WM_HOTKEY = 0x0312, WM_POWERBROADCAST = 0x0218, PBT_POWERSETTINGCHANGE = 0x8013;
        if (m.Msg == WM_HOTKEY)
        {
            switch ((int)m.WParam)
            {
                case HkToggle: SetLights(!_engine.LightsOn); break;
                case HkNext: StepEffect(+1); break;
                case HkPrev: StepEffect(-1); break;
                case HkUp: SetBrightness(_bright.Value + 10); break;
                case HkDown: SetBrightness(_bright.Value - 10); break;
            }
        }
        else if (m.Msg == WM_POWERBROADCAST && (int)m.WParam == PBT_POWERSETTINGCHANGE && m.LParam != IntPtr.Zero)
        {
            int state = Marshal.ReadInt32(m.LParam, 20); // 0 kapalı, 1 açık, 2 kısık
            _displayOff = state == 0;
            UpdateIdlePause();
        }
        base.WndProc(ref m);
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        switch (e.Mode)
        {
            case PowerModes.Suspend:
                _engine.SetPause(PauseReason.Suspended, true);
                break;
            case PowerModes.Resume:
                _engine.SetPause(PauseReason.Suspended, false); // bağlantıyı yeniler ve rengi tekrar yollar
                break;
            case PowerModes.StatusChange:
                _engine.OnBattery = SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Offline;
                break;
        }
    }

    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        if (e.Reason is SessionSwitchReason.SessionLock or SessionSwitchReason.ConsoleDisconnect)
        {
            _locked = true;
            _engine.SessionLocked = true; // ekran efektleri kilit ekranında son rengi korur
            UpdateIdlePause();
            _engine.ForceRefresh();
        }
        else if (e.Reason is SessionSwitchReason.SessionUnlock or SessionSwitchReason.ConsoleConnect)
        {
            _locked = false;
            _engine.SessionLocked = false;
            UpdateIdlePause();
            _engine.ForceRefresh();
        }
    }

    private bool _locked, _displayOff, _idleActive;

    /// <summary>
    /// Kilitliyken ya da ekran kendiliğinden kapandığında, seçilen süre (Hemen / 1 dk / 5 dk / Asla) sonunda ışığı söndürür.
    /// Kilit açılınca / ekran geri gelince hemen yakar.
    /// </summary>
    private void UpdateIdlePause(bool restart = false)
    {
        bool idle = _locked || _displayOff;
        if (!idle)
        {
            _idleTimer.Stop();
            if (_idleActive || _engine.IsPaused) _engine.SetPause(PauseReason.Locked, false);
            _idleActive = false;
            return;
        }
        if (_idleActive && !restart) return; // zaten sayıyor ya da kapalı (kilit + ekran kapanması üst üste)
        _idleActive = true;
        _idleTimer.Stop();
        int delay = IdleDelayMs[Math.Clamp(_s.IdleOff, 0, IdleDelayMs.Length - 1)];
        if (delay < 0) _engine.SetPause(PauseReason.Locked, false);       // Asla
        else if (delay == 0) _engine.SetPause(PauseReason.Locked, true);  // Hemen
        else
        {
            _engine.SetPause(PauseReason.Locked, false);
            _idleTimer.Interval = delay;
            _idleTimer.Start();
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_allowClose && _s.MinimizeToTray && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            if (!_s.TrayHintShown)
            {
                _s.TrayHintShown = true;
                Changed();
                _tray.ShowBalloonTip(3000, "Klavye Işık Stüdyosu", "Arka planda çalışmaya devam ediyor. Simgeye tıklayarak paneli açabilirsin.", ToolTipIcon.Info);
            }
            return;
        }
        Cleanup();
        base.OnFormClosing(e);
    }

    private void ExitApp()
    {
        _allowClose = true;
        Close();
    }

    private bool _cleaned;

    private void Cleanup()
    {
        if (_cleaned) return;
        _cleaned = true;
        _s.Save();
        _uiTimer.Stop();
        _idleTimer.Stop();
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        _showReg?.Unregister(null);
        _showEvent?.Dispose();
        for (int i = 1; i <= 5; i++) Native.UnregisterHotKey(Handle, i);
        if (_displayNotify != IntPtr.Zero) Native.UnregisterPowerSettingNotification(_displayNotify);
        _hook?.Dispose();
        _tray.Visible = false;

        // Çıkarken klavyeyi sabit bir renkte bırak (animasyon ortada donmasın).
        Color? final = null;
        if (_engine.LightsOn)
        {
            var eff = _engine.Effect;
            var baseColor = eff.Colors >= 1 ? _chip1.Color : _engine.CurrentColor;
            float scale = MathF.Pow(_bright.Value / 100f, 1.6f);
            final = eff.Colors >= 1
                ? Color.FromArgb((int)(baseColor.R * scale), (int)(baseColor.G * scale), (int)(baseColor.B * scale))
                : baseColor;
        }
        else
        {
            final = Color.Black;
        }
        _engine.Stop(final);
        _engine.Dispose();
        _kb.Dispose();
    }
}
