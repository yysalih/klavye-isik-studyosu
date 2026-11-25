using System.Buffers.Binary;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace KlavyeIsik;

/// <summary>Klavye arka ışığının türü (BIOS'tan okunur).</summary>
public enum KbType
{
    Unknown,   // okunamadı: tek bölge RGB varsayılır
    None,      // arka ışık yok
    White,     // tek renk (beyaz) — renk değiştirilemez
    OneZone,   // tek bölge RGB — tam destek
    ThreeZone, // 3 bölge RGB — üç bölge aynı renge ayarlanır
    PerKey,    // tuş başına RGB (ITE USB) — desteklenmez
}

public enum KbBackend { None, Dchu, Dll, Wmi }

/// <summary>
/// Clevo tabanlı dizüstülerin (Monster, Tuxedo, XMG, Sager...) klavye ışığı.
/// Komut numaraları her yolda aynıdır (0x67 = renk, 0x0D = bilgi tamponu, 0x52 = özellik listesi):
///  1) Doğrudan sürücü: Insyde DCHU sürücüsünün ACPI\CLV0001 aygıtına _DSM çağrısı (Control Center 3.x, ~2019+). Yönetici gerekmez.
///  2) InsydeDCHU.dll'in SetDCHU_Data fonksiyonu (aynı sürücü, yedek yol).
///  3) Eski WMI arayüzü: root\WMI CLEVO_GET.SetKBLED (Control Center 2.x, ~2015–2019). Yönetici izni gerekir.
/// Renk biçimi: bölge | B&lt;&lt;16 | R&lt;&lt;8 | G. Bölgeler: 0xF0 (tek bölge / sol), 0xF1 (orta), 0xF2 (sağ).
/// Kaynak: tuxedo-drivers (clevo_leds.h, clevo_acpi.c, clevo_wmi.c) ve kendi ölçümlerimiz.
/// </summary>
public sealed class ClevoKeyboard : IDisposable
{
    private static Guid InterfaceGuid = new("86994c74-ad43-4812-b7e7-0c420b5c5fd7");
    private static Guid HidInterfaceGuid = new("4d1e55b2-f16f-11cf-88cb-001111000030");
    private static readonly Guid DsmGuid = new("93f224e4-fbdc-4bbf-add6-db71bdc0afad");
    private const uint IoctlEval = 0x322400;
    private const uint CmdSetKbLed = 0x67, CmdInfoBuffer = 0x0D, CmdFeatures = 0x52;
    private const int CmdOffset = 24, ArgOffset = 34;

    private readonly byte[] _in = new byte[1056];
    private readonly byte[] _out = new byte[1036];
    private SafeFileHandle? _handle;

    // Yedek yol: InsydeDCHU.dll
    private IntPtr _dll;
    private unsafe delegate* unmanaged[Stdcall]<uint, uint*, uint, uint> _setDchu;
    private unsafe delegate* unmanaged[Stdcall]<int, byte*, int> _getDchuBuffer;

    // Eski yol: WMI
    private ManagementObject? _wmi;
    private ManagementBaseObject? _wmiParams;

    private bool _typeKnown;

    public string Status { get; private set; } = "Bağlanılmadı";
    public KbBackend Backend { get; private set; }
    public KbType Type { get; private set; } = KbType.Unknown;

    /// <summary>Klavye bulundu ama bu program onu süremez (beyaz, tuş başına RGB, ışıksız).</summary>
    public bool Unsupported => Type is KbType.White or KbType.PerKey or KbType.None;

    /// <summary>Eski (WMI) arayüz bulundu ama yönetici izni olmadan çalışmıyor.</summary>
    public bool NeedsAdmin { get; private set; }

    /// <summary>Bu bağlantı yolunun kaldırabileceği en yüksek kare hızı (0 = sınırsız).</summary>
    public int MaxFps => Backend == KbBackend.Wmi ? 10 : 0;

    public bool IsOpen => Backend != KbBackend.None;

    public static bool IsAdmin
    {
        get
        {
            try { return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator); }
            catch { return false; }
        }
    }

    public string TypeText => Type switch
    {
        KbType.OneZone => "Tek bölge RGB",
        KbType.ThreeZone => "3 bölge RGB (tek renk olarak)",
        KbType.White => "Tek renk (beyaz) klavye — renk değiştirilemez",
        KbType.PerKey => "Tuş başına RGB klavye — desteklenmiyor",
        KbType.None => "Klavyede arka ışık yok",
        _ => "RGB klavye",
    };

    public ClevoKeyboard()
    {
        DsmGuid.TryWriteBytes(_in.AsSpan(0, 16));
        Encoding.ASCII.GetBytes("_DSM").CopyTo(_in, 20);
        BinaryPrimitives.WriteUInt32LittleEndian(_in.AsSpan(CmdOffset), CmdSetKbLed);
        _in[28] = 4; _in[29] = 1; _in[30] = 2; _in[31] = 0; _in[32] = 0; _in[33] = 1;
    }

    // ================================================================== Bağlantı

    public bool Open()
    {
        Close();
        NeedsAdmin = false;

        if (TryOpenDchu()) { Backend = KbBackend.Dchu; Status = "Doğrudan sürücü bağlantısı"; }
        else if (TryLoadDll()) { Backend = KbBackend.Dll; Status = "InsydeDCHU.dll üzerinden"; }
        else if (TryOpenWmi()) { Backend = KbBackend.Wmi; Status = "Eski WMI arayüzü (yönetici)"; }
        else
        {
            if (!_typeKnown && HasPerKeyUsbKeyboard()) { Type = KbType.PerKey; _typeKnown = true; }
            Status = NeedsAdmin ? "Bu model için yönetici izni gerekli"
                   : Unsupported ? TypeText
                   : LooksLikeTongfang() ? "Tongfang (Uniwill) tabanlı model — henüz desteklenmiyor"
                   : "Klavye sürücüsü bulunamadı (Control Center kurulu mu?)";
            return false;
        }

        if (!_typeKnown) { Type = DetectType(); _typeKnown = true; }
        if (Unsupported)
        {
            Status = TypeText;
            Close();
            return false;
        }
        return true;
    }

    private bool TryOpenDchu()
    {
        var path = FindDevicePath(ref InterfaceGuid);
        if (path == null) return false;
        const uint GENERIC_RW = 0xC0000000, SHARE_RW = 3, OPEN_EXISTING = 3, NORMAL = 0x80;
        var h = Native.CreateFileW(path, GENERIC_RW, SHARE_RW, IntPtr.Zero, OPEN_EXISTING, NORMAL, IntPtr.Zero);
        if (h.IsInvalid) { h.Dispose(); return false; }
        _handle = h;
        return true;
    }

    private static string? FindDevicePath(ref Guid guid) => FindDevicePaths(ref guid).FirstOrDefault();

    private static string[] FindDevicePaths(ref Guid guid)
    {
        try
        {
            if (Native.CM_Get_Device_Interface_List_SizeW(out int len, ref guid, IntPtr.Zero, 0) != 0 || len <= 1)
                return Array.Empty<string>();
            var buf = new char[len];
            if (Native.CM_Get_Device_Interface_ListW(ref guid, IntPtr.Zero, buf, len, 0) != 0)
                return Array.Empty<string>();
            return new string(buf).Split('\0', StringSplitOptions.RemoveEmptyEntries);
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private unsafe bool TryLoadDll()
    {
        var dirs = new List<string> { AppContext.BaseDirectory };
        try
        {
            // Sürücü paketi DLL'i DriverStore'a koyar (insydedchu.inf_amd64_xxxx).
            var store = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "DriverStore", "FileRepository");
            if (Directory.Exists(store)) dirs.AddRange(Directory.EnumerateDirectories(store, "insydedchu*"));
        }
        catch { }

        foreach (var dir in dirs)
        {
            var p = Path.Combine(dir, "InsydeDCHU.dll");
            if (!File.Exists(p) || !NativeLibrary.TryLoad(p, out var lib)) continue;
            if (NativeLibrary.TryGetExport(lib, "SetDCHU_Data", out var fn))
            {
                _dll = lib;
                _setDchu = (delegate* unmanaged[Stdcall]<uint, uint*, uint, uint>)fn;
                if (NativeLibrary.TryGetExport(lib, "GetDCHU_Data_Buffer", out var gb))
                    _getDchuBuffer = (delegate* unmanaged[Stdcall]<int, byte*, int>)gb;
                return true;
            }
            NativeLibrary.Free(lib);
        }
        return false;
    }

    private bool TryOpenWmi()
    {
        try
        {
            var scope = new ManagementScope(@"root\WMI");
            scope.Connect();
            using var searcher = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT * FROM CLEVO_GET"));
            foreach (ManagementObject o in searcher.Get())
            {
                // Erişimi okuma komutuyla dene (yönetici değilsek burada "erişim engellendi" gelir).
                o.InvokeMethod("BIOS_special_feature_list", null, null);
                _wmi = o;
                _wmiParams = o.GetMethodParameters("SetKBLED");
                return true;
            }
        }
        catch (ManagementException e) when (e.ErrorCode == ManagementStatus.AccessDenied) { NeedsAdmin = !IsAdmin; }
        catch (UnauthorizedAccessException) { NeedsAdmin = !IsAdmin; }
        catch { /* sınıf yok: eski sürücü kurulu değil */ }
        _wmi?.Dispose();
        _wmi = null;
        return false;
    }

    // ================================================================== Klavye türü

    private KbType DetectType()
    {
        // 1) 0x0D bilgi tamponu: 0x0F. bayt klavye türü (tuxedo-drivers clevo_leds_init ile aynı).
        for (int i = 0; i < 3; i++)
        {
            int t = ReadInfoByte(0x0F);
            if (t < 0) break;           // bu yol desteklemiyor
            if (t > 0)
            {
                return t switch
                {
                    0x01 => KbType.White,
                    0x02 => KbType.ThreeZone,
                    0x06 => KbType.OneZone,
                    0xF3 => KbType.PerKey,
                    _ => KbType.Unknown,
                };
            }
            Thread.Sleep(50);
        }

        if (HasPerKeyUsbKeyboard()) return KbType.PerKey;

        // 2) Eski makineler (~8. nesil ve öncesi): 0x52 özellik listesi.
        uint f = ReadInteger(CmdFeatures);
        if (f != 0 && f != 0xFFFFFFFF)
        {
            if ((f & 0x00400000) != 0) return KbType.ThreeZone;
            if ((f & 0x40000000) != 0) return KbType.White;
        }
        return KbType.Unknown;
    }

    /// <summary>0x0D tamponundan bir bayt; okunamazsa -1.</summary>
    private unsafe int ReadInfoByte(int index)
    {
        try
        {
            if (Backend == KbBackend.Dchu && Call(CmdInfoBuffer, 0, out var data, out bool isBuffer) && isBuffer)
                return index < data.Length ? data[index] : -1;

            if (Backend == KbBackend.Dll && _getDchuBuffer != null)
            {
                var buf = stackalloc byte[1024];
                new Span<byte>(buf, 1024).Clear();
                _getDchuBuffer((int)CmdInfoBuffer, buf);
                return buf[index];
            }
        }
        catch { }
        return -1;
    }

    private uint ReadInteger(uint cmd)
    {
        try
        {
            if (Backend == KbBackend.Dchu && Call(cmd, 0, out var data, out bool isBuffer) && !isBuffer && data.Length >= 4)
                return BinaryPrimitives.ReadUInt32LittleEndian(data);

            if (Backend == KbBackend.Wmi && _wmi != null && cmd == CmdFeatures)
            {
                using var r = _wmi.InvokeMethod("BIOS_special_feature_list", null, null);
                return Convert.ToUInt32(r?["Data"] ?? 0u);
            }
        }
        catch { }
        return 0;
    }

    /// <summary>
    /// _DSM çağrısı. Çıkış ACPI_EVAL_OUTPUT_BUFFER biçiminde: "BoeA" imzası, uzunluk, argüman sayısı,
    /// sonra ilk argüman: tür (0 = tamsayı, 2 = tampon), veri uzunluğu, veri (16. bayttan itibaren).
    /// </summary>
    private bool Call(uint cmd, uint arg, out byte[] data, out bool isBuffer)
    {
        data = Array.Empty<byte>();
        isBuffer = false;
        if (_handle is not { IsInvalid: false, IsClosed: false }) return false;

        BinaryPrimitives.WriteUInt32LittleEndian(_in.AsSpan(CmdOffset), cmd);
        BinaryPrimitives.WriteUInt32LittleEndian(_in.AsSpan(ArgOffset), arg);
        Array.Clear(_out);
        bool ok = Native.DeviceIoControl(_handle, IoctlEval, _in, _in.Length, _out, _out.Length, out int ret, IntPtr.Zero);
        BinaryPrimitives.WriteUInt32LittleEndian(_in.AsSpan(CmdOffset), CmdSetKbLed); // renk komutuna geri dön
        if (!ok || ret < 16 || BinaryPrimitives.ReadUInt32LittleEndian(_out) != 0x426F6541 /*BoeA*/) return false;

        int type = BinaryPrimitives.ReadUInt16LittleEndian(_out.AsSpan(12));
        int len = BinaryPrimitives.ReadUInt16LittleEndian(_out.AsSpan(14));
        len = Math.Clamp(len, 0, Math.Min(ret, _out.Length) - 16);
        data = _out.AsSpan(16, len).ToArray();
        isBuffer = type == 2;
        return true;
    }

    /// <summary>
    /// Bazı Monster modelleri (ör. Tulpar T7 V20.6, bazı Abra V11–V15 / Tulpar T5 V18–V19) Clevo değil Tongfang kasasıdır;
    /// klavye ışığı orada Uniwill denetleyicisi ile sürülür ve bu program onu desteklemez.
    /// </summary>
    private static bool LooksLikeTongfang()
    {
        try
        {
            using var k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\BIOS");
            foreach (var name in new[] { "BaseBoardProduct", "BaseBoardManufacturer", "SystemSKU", "SystemVersion", "SystemProductName" })
            {
                var v = (k?.GetValue(name) as string)?.Trim() ?? "";
                if (v.Contains("TONGFANG", StringComparison.OrdinalIgnoreCase) || v.Contains("UNIWILL", StringComparison.OrdinalIgnoreCase))
                    return true;
                // Tongfang kart kodları: GK5CN6Z, GM7PG0R, GK5MR0V, GX5... gibi
                if (System.Text.RegularExpressions.Regex.IsMatch(v, @"^G[KMXIP]\d[A-Z0-9]{3,}$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                    return true;
            }
        }
        catch { }
        return false;
    }

    /// <summary>Tuş başına RGB Clevo klavyeleri USB üzerinden ITE denetleyicisiyle gelir.</summary>
    private static bool HasPerKeyUsbKeyboard()
    {
        string[] ids = { "VID_048D&PID_6004", "VID_048D&PID_600A", "VID_048D&PID_600B", "VID_048D&PID_CE00", "VID_048D&PID_8910" };
        foreach (var p in FindDevicePaths(ref HidInterfaceGuid))
            foreach (var id in ids)
                if (p.Contains(id, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    // ================================================================== Renk

    public unsafe bool Send(uint code)
    {
        switch (Backend)
        {
            case KbBackend.Dchu when _handle is { IsInvalid: false, IsClosed: false }:
                BinaryPrimitives.WriteUInt32LittleEndian(_in.AsSpan(ArgOffset), code);
                return Native.DeviceIoControl(_handle, IoctlEval, _in, _in.Length, _out, _out.Length, out _, IntPtr.Zero);

            case KbBackend.Dll when _setDchu != null:
                uint c = code;
                _setDchu(CmdSetKbLed, &c, 4);
                return true;

            case KbBackend.Wmi when _wmi != null && _wmiParams != null:
                try
                {
                    _wmiParams["Data"] = code;
                    _wmi.InvokeMethod("SetKBLED", _wmiParams, null)?.Dispose();
                    return true;
                }
                catch { return false; }
        }
        return false;
    }

    /// <summary>Tüm klavyeyi tek renge ayarlar. 3 bölgeli klavyede üç bölge de aynı renge ayarlanır.</summary>
    public bool SetColor(int r, int g, int b)
    {
        uint rgb = ((uint)(b & 0xFF) << 16) | ((uint)(r & 0xFF) << 8) | (uint)(g & 0xFF);
        if (Type == KbType.ThreeZone)
            return Send(0xF0000000u | rgb) & Send(0xF1000000u | rgb) & Send(0xF2000000u | rgb);
        return Send(0xF0000000u | rgb);
    }

    public unsafe void Close()
    {
        _handle?.Dispose();
        _handle = null;
        if (_dll != IntPtr.Zero)
        {
            _setDchu = null;
            _getDchuBuffer = null;
            NativeLibrary.Free(_dll);
            _dll = IntPtr.Zero;
        }
        _wmiParams?.Dispose();
        _wmiParams = null;
        _wmi?.Dispose();
        _wmi = null;
        Backend = KbBackend.None;
    }

    public void Dispose() => Close();
}
