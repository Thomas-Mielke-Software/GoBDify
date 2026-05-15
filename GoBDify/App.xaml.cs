using GoBDify.Services;

namespace GoBDify;

public partial class App : Application
{
    public App()
    {
        AppDomain.CurrentDomain.UnhandledException += (s, e) => LogFatal((Exception)e.ExceptionObject);
        TaskScheduler.UnobservedTaskException += (s, e) => LogFatal(e.Exception);

        try
        {
            InitializeComponent();
            MainPage = new AppShell();
        }
        catch (Exception ex)
        {
            LogFatal(ex);
            throw;
        }
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = base.CreateWindow(activationState);
        var ws = IPlatformApplication.Current?.Services.GetService<WorkspaceService>();
        var s = ws?.Settings;
        if (s != null)
        {
            if (s.WindowWidth is { } w  && w  >= 400) window.Width  = w;
            if (s.WindowHeight is { } h && h >= 300) window.Height = h;
            if (s.WindowX is { } x) window.X = x;
            if (s.WindowY is { } y) window.Y = y;
        }
        window.Stopped += SaveWindowGeometry;
        window.Destroying += SaveWindowGeometry;
        return window;

        void SaveWindowGeometry(object? sender, EventArgs e)
        {
            try
            {
                var ws2 = IPlatformApplication.Current?.Services.GetService<WorkspaceService>();
                if (ws2 == null || sender is not Window win) return;
                ws2.Settings.WindowX = win.X;
                ws2.Settings.WindowY = win.Y;
                ws2.Settings.WindowWidth = win.Width;
                ws2.Settings.WindowHeight = win.Height;
                ws2.SaveSettings();
            }
            catch { }
        }
    }

    private static void LogFatal(Exception ex)
    {
        try
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GoBDify-startup-crash.log");
            File.AppendAllText(path,
                $"[{DateTime.Now:u}] {ex.GetType().FullName}: {ex.Message}\n{ex.StackTrace}\n\n");
        }
        catch { }
    }
}
