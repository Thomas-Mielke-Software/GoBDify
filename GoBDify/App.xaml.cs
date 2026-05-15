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
