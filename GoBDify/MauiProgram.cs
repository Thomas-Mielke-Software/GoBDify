using CommunityToolkit.Maui;
using GoBDify.Services;
using Microsoft.Extensions.Logging;

namespace GoBDify
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            try
            {
                AppDomain.CurrentDomain.UnhandledException += (s, e) => LogFatal((Exception)e.ExceptionObject, "AppDomain.UnhandledException");

                var builder = MauiApp.CreateBuilder();
                builder
                    .UseMauiApp<App>()
                    .UseMauiCommunityToolkit()
                    .ConfigureFonts(fonts =>
                    {
                        fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                        fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    });

                builder.Services.AddSingleton<IFolderPicker, WindowsFolderPicker>();
                builder.Services.AddSingleton<WorkspaceService>();

#if DEBUG
                builder.Logging.AddDebug();
#endif

                return builder.Build();
            }
            catch (Exception ex)
            {
                LogFatal(ex, "CreateMauiApp");
                throw;
            }
        }

        internal static void LogFatal(Exception ex, string where)
        {
            try
            {
                var path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "GoBDify-startup-crash.log");
                File.AppendAllText(path,
                    $"[{DateTime.Now:u}] ({where}) {ex.GetType().FullName}: {ex.Message}\n{ex}\n\n");
            }
            catch { }
        }
    }
}
