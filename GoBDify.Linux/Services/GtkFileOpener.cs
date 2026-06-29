namespace GoBDify.Services;

/// <summary>
/// Linux/GTK-Implementierung von <see cref="IFileOpener"/> über den nativen
/// <c>Gtk.FileDialog</c> im Öffnen-Modus (GTK 4.10+). Spiegelt das Muster von
/// <see cref="GtkFileSaver"/>.
/// </summary>
public sealed class GtkFileOpener : IFileOpener
{
    public async Task<string?> OpenTextAsync(CancellationToken ct = default)
    {
        var dialog = Gtk.FileDialog.New();
        dialog.SetTitle("Ordnerliste importieren");
        dialog.SetModal(true);

        var window = GetActiveWindow();
        if (window is null) return null;

        try
        {
            var file = await dialog.OpenAsync(window);
            var path = file?.GetPath();
            if (path is null) return null;

            return await File.ReadAllTextAsync(path, ct);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (GLib.GException ex) when (ex.Message.Contains("dismissed", StringComparison.OrdinalIgnoreCase))
        {
            // Benutzer hat den Dialog abgebrochen.
            return null;
        }
    }

    private static Gtk.Window? GetActiveWindow()
    {
        if (Gtk.Application.GetDefault() is Gtk.Application app && app.GetActiveWindow() is Gtk.Window activeWindow)
            return activeWindow;

        var toplevels = Gtk.Window.GetToplevels();
        for (uint i = 0; i < toplevels.GetNItems(); i++)
        {
            if (toplevels.GetObject(i) is Gtk.Window window)
                return window;
        }
        return null;
    }
}
