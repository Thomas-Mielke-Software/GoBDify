using Microsoft.Maui.Hosting;
using Platform.Maui.Linux.Gtk4.Platform;

namespace GoBDify;

public class Program : GtkMauiApplication
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    public static void Main(string[] args)
    {
        var app = new Program();
        app.Run(args);
    }
}
