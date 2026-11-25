namespace KlavyeIsik;

internal static class Program
{
    public const string ShowEventName = "KlavyeIsik.PaneliGoster";

    [STAThread]
    private static void Main(string[] args)
    {
        using var mutex = new Mutex(true, "KlavyeIsik.TekOrnek", out bool first);
        if (!first && args.Any(a => a.Equals("--elevated", StringComparison.OrdinalIgnoreCase)))
        {
            // Yönetici olarak yeniden başlatıldık: eski örneğin kapanmasını bekle.
            try { first = mutex.WaitOne(8000); }
            catch (AbandonedMutexException) { first = true; }
        }
        if (!first)
        {
            // Zaten çalışıyor: mevcut pencereyi öne getir.
            try
            {
                using var ev = EventWaitHandle.OpenExisting(ShowEventName);
                ev.Set();
            }
            catch { }
            return;
        }

        ApplicationConfiguration.Initialize();
        bool hidden = args.Any(a => a.Equals("--tray", StringComparison.OrdinalIgnoreCase));

        // Geliştirme/test: --snapshot <dosya.png> pencerenin görüntüsünü kaydedip çıkar.
        int snap = Array.FindIndex(args, a => a.Equals("--snapshot", StringComparison.OrdinalIgnoreCase));
        if (snap >= 0 && snap + 1 < args.Length)
        {
            var form = new MainForm(false);
            var t = new System.Windows.Forms.Timer { Interval = 1500 };
            t.Tick += (_, _) =>
            {
                t.Stop();
                using var bmp = new Bitmap(form.Width, form.Height);
                form.DrawToBitmap(bmp, new Rectangle(0, 0, form.Width, form.Height));
                bmp.Save(args[snap + 1]);
                form.Close();
                Application.Exit();
            };
            form.Shown += (_, _) => t.Start();
            Application.Run(form);
            return;
        }

        Application.Run(new MainForm(hidden));
    }
}
