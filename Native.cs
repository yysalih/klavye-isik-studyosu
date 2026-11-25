using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace KlavyeIsik;

internal static class Native
{
    // ---- Dosya / sürücü ----
    [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr sa,
        uint disposition, uint flags, IntPtr template);

    [DllImport("kernel32", SetLastError = true)]
    public static extern bool DeviceIoControl(SafeFileHandle h, uint code, byte[] inBuf, int inSize,
        byte[] outBuf, int outSize, out int returned, IntPtr overlapped);

    [DllImport("cfgmgr32", CharSet = CharSet.Unicode)]
    public static extern int CM_Get_Device_Interface_List_SizeW(out int len, ref Guid iface, IntPtr devId, int flags);

    [DllImport("cfgmgr32", CharSet = CharSet.Unicode)]
    public static extern int CM_Get_Device_Interface_ListW(ref Guid iface, IntPtr devId, char[] buffer, int len, int flags);

    // ---- Yüksek çözünürlüklü bekleme zamanlayıcısı (sistem saat çözünürlüğünü değiştirmez) ----
    [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr CreateWaitableTimerExW(IntPtr attr, string? name, uint flags, uint access);

    [DllImport("kernel32", SetLastError = true)]
    public static extern bool SetWaitableTimer(SafeWaitHandle h, ref long dueTime, int period, IntPtr cb, IntPtr arg, bool resume);

    // ---- Verimlilik modu (EcoQoS) ----
    [StructLayout(LayoutKind.Sequential)]
    public struct THREAD_POWER_THROTTLING_STATE { public uint Version, ControlMask, StateMask; }

    [DllImport("kernel32")]
    public static extern bool SetThreadInformation(IntPtr thread, int infoClass, ref THREAD_POWER_THROTTLING_STATE info, int size);

    [DllImport("kernel32")]
    public static extern IntPtr GetCurrentThread();

    // ---- CPU ----
    [DllImport("kernel32")]
    public static extern bool GetSystemTimes(out long idle, out long kernel, out long user);

    // ---- Kısayol tuşları ----
    [DllImport("user32", SetLastError = true)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mods, uint vk);

    [DllImport("user32")]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    // ---- Klavye kancası (yalnızca "tuşa basıldı" sayılır, hangi tuş olduğu kaydedilmez) ----
    public delegate IntPtr LowLevelKeyboardProc(int code, IntPtr wParam, IntPtr lParam);

    [DllImport("user32", SetLastError = true)]
    public static extern IntPtr SetWindowsHookExW(int idHook, LowLevelKeyboardProc fn, IntPtr hMod, uint threadId);

    [DllImport("user32")]
    public static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32")]
    public static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32", CharSet = CharSet.Unicode)]
    public static extern IntPtr GetModuleHandleW(string? name);

    // ---- Ekran açık/kapalı bildirimi ----
    [DllImport("user32", SetLastError = true)]
    public static extern IntPtr RegisterPowerSettingNotification(IntPtr hRecipient, ref Guid setting, int flags);

    [DllImport("user32")]
    public static extern bool UnregisterPowerSettingNotification(IntPtr handle);

    // ---- Ekran örnekleme (GDI) ----
    [StructLayout(LayoutKind.Sequential)]
    public struct BITMAPINFOHEADER
    {
        public int biSize, biWidth, biHeight;
        public short biPlanes, biBitCount;
        public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant;
    }

    [DllImport("user32")] public static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32")] public static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);
    [DllImport("user32")] public static extern int GetSystemMetrics(int index);
    [DllImport("gdi32")] public static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32")] public static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32")] public static extern IntPtr CreateDIBSection(IntPtr dc, ref BITMAPINFOHEADER bmi, uint usage, out IntPtr bits, IntPtr section, uint offset);
    [DllImport("gdi32")] public static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
    [DllImport("gdi32")] public static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32")] public static extern int SetStretchBltMode(IntPtr dc, int mode);
    [DllImport("gdi32")] public static extern bool StretchBlt(IntPtr dst, int x, int y, int w, int h, IntPtr src, int sx, int sy, int sw, int sh, uint rop);
    [DllImport("gdi32")] public static extern bool GdiFlush();

    // ---- Bellek ----
    [DllImport("psapi")]
    public static extern bool EmptyWorkingSet(IntPtr process);

    // ---- Koyu tema ----
    [DllImport("dwmapi")]
    public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    [DllImport("uxtheme", CharSet = CharSet.Unicode)]
    public static extern int SetWindowTheme(IntPtr hwnd, string? appName, string? idList);
}

/// <summary>Yüksek çözünürlüklü bekleme zamanlayıcısını WaitHandle olarak sarar.</summary>
internal sealed class TimerWaitHandle : WaitHandle
{
    private TimerWaitHandle(IntPtr h) => SafeWaitHandle = new SafeWaitHandle(h, true);

    public static TimerWaitHandle? Create()
    {
        const uint TIMER_ALL_ACCESS = 0x1F0003;
        const uint HIGH_RESOLUTION = 0x2;
        var h = Native.CreateWaitableTimerExW(IntPtr.Zero, null, HIGH_RESOLUTION, TIMER_ALL_ACCESS);
        if (h == IntPtr.Zero) h = Native.CreateWaitableTimerExW(IntPtr.Zero, null, 0, TIMER_ALL_ACCESS);
        return h == IntPtr.Zero ? null : new TimerWaitHandle(h);
    }

    public void Arm(int ms)
    {
        long due = -(long)ms * 10_000;
        Native.SetWaitableTimer(SafeWaitHandle, ref due, 0, IntPtr.Zero, IntPtr.Zero, false);
    }
}
