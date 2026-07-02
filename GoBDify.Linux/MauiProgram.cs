using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Platforms.Linux.Gtk4.Hosting;
using Microsoft.Maui.Platforms.Linux.Gtk4.Essentials.Hosting;
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

        // Eigener Shell-Handler: rendert die Ordnerverwaltung in den Flyout, den das
        // GTK4-Backend sonst nur als Item-Liste (Übersicht/Einstellungen) zeichnet.
        builder.ConfigureMauiHandlers(handlers =>
            handlers.AddHandler<Microsoft.Maui.Controls.Shell, Platform.FolderFlyoutShellHandler>());

        builder.ConfigureFonts(fonts =>
        {
            fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
        });

        // Plattform-Implementierung der Ordnerauswahl + Datei-Speicherung (GTK FileDialog).
        builder.Services.AddSingleton<IFolderPicker, GtkFolderPicker>();
        builder.Services.AddSingleton<IFileSaver, GtkFileSaver>();
        builder.Services.AddSingleton<IFileOpener, GtkFileOpener>();
        builder.Services.AddSingleton<WorkspaceService>();

        var app = builder.Build();

        // GTK4 setzt keinen SynchronizationContext auf dem UI-Thread — ohne ihn
        // laufen Progress<T>-Callbacks und await-Fortsetzungen off-thread und
        // crashen beim UI-Zugriff. Wir installieren einen Dispatcher-gestützten.
        var dispatcher = app.Services.GetRequiredService<IDispatcher>();
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));

        return app;
    }
}
