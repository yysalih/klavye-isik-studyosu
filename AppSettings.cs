using System.Text.Json;
using Microsoft.Win32;

namespace KlavyeIsik;

public sealed class AppSettings
{
    public string Effect { get; set; } = "nefes";
    public string Color1 { get; set; } = "#008CFF";
    public string Color2 { get; set; } = "#FF0078";
    public int Brightness { get; set; } = 85;
    public int Speed { get; set; } = 100;
    public int Performance { get; set; } = 1;
    public bool LightsOn { get; set; } = true;
    public bool MinimizeToTray { get; set; } = true;
    public bool EcoOnBattery { get; set; } = true;
    /// <summary>Kilit / ekran kapalıyken ışığı kapatma: 0 hemen, 1 = 1 dk, 2 = 5 dk, 3 asla.</summary>
    public int IdleOff { get; set; } = 1;
    public bool TrayHintShown { get; set; }

    private static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KlavyeIsik");
    private static string FilePath => Path.Combine(Dir, "ayarlar.json");
    private static readonly JsonSerializerOptions Opts = new() { WriteIndented = true };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Opts) ?? new AppSettings();
        }
        catch { /* bozuk dosya: varsayılanlar */ }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Opts));
        }
        catch { }
    }

    public static Color ParseColor(string? hex, Color fallback)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(hex))
            {
                var v = Convert.ToInt32(hex.Trim().TrimStart('#'), 16);
                return Color.FromArgb((v >> 16) & 255, (v >> 8) & 255, v & 255);
            }
        }
        catch { }
        return fallback;
    }

    public static string ToHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";
}

/// <summary>
/// Windows açılışında başlatma.
/// Normalde HKCU\...\Run kullanılır (yönetici gerekmez). Eski WMI arayüzlü modeller yönetici izni istediği için
/// orada "en yüksek ayrıcalıkla" çalışan bir oturum açma görevi (Görev Zamanlayıcı) oluşturulur.
/// </summary>
public static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Name = "KlavyeIsik";

    private static string Command => $"\"{Environment.ProcessPath}\" --tray";

    public static bool IsEnabled
    {
        get
        {
            try
            {
                using var k = Registry.CurrentUser.OpenSubKey(RunKey);
                if (k?.GetValue(Name) is string s && s.Contains(Environment.ProcessPath ?? "?", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            catch { }
            return TaskExists();
        }
    }

    public static void Set(bool on, bool elevated = false)
    {
        try
        {
            using var k = Registry.CurrentUser.CreateSubKey(RunKey);
            if (on && !elevated) k.SetValue(Name, Command);
            else k.DeleteValue(Name, false);
        }
        catch { }

        if (on && elevated && ClevoKeyboard.IsAdmin)
            Schtasks($"/Create /F /TN \"{Name}\" /SC ONLOGON /RL HIGHEST /TR \"\\\"{Environment.ProcessPath}\\\" --tray\"");
        else if (!on || !elevated)
        {
            if (TaskExists()) Schtasks($"/Delete /F /TN \"{Name}\"");
        }
    }

    private static bool TaskExists() => Schtasks($"/Query /TN \"{Name}\"") == 0;

    private static int Schtasks(string args)
    {
        try
        {
            using var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("schtasks.exe", args)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            if (p == null) return -1;
            p.WaitForExit(5000);
            return p.HasExited ? p.ExitCode : -1;
        }
        catch { return -1; }
    }
}
