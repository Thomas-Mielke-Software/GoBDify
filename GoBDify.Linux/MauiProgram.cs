using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Hosting;
using Platform.Maui.Linux.Gtk4.Hosting;
using Platform.Maui.Linux.Gtk4.Essentials.Hosting;
using GoBDify.Services;

namespace GoBDify;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp
            .CreateBuilder()
            .UseMauiAppLinuxGtk4<App>()
            .AddLinuxGtk4Essentials();

        builder.ConfigureFonts(fonts =>
        {
            fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
        });

        // Plattform-Implementierung der Ordnerauswahl (GTK FileDialog).
        builder.Services.AddSingleton<IFolderPicker, GtkFolderPicker>();
        builder.Services.AddSingleton<WorkspaceService>();

        return builder.Build();
    }
}
