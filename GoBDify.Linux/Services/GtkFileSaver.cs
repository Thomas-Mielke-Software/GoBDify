namespace GoBDify.Services;

/// <summary>
/// Linux/GTK-Implementierung von <see cref="IFileSaver"/> über den nativen
/// <c>Gtk.FileDialog</c> im Speichern-Modus (GTK 4.10+). Spiegelt das Muster von
/// <see cref="GtkFolderPicker"/>.
/// </summary>
public sealed class GtkFileSaver : IFileSaver
{
    public async Task<string?> SaveTextAsync(string suggestedFileName, string content, CancellationToken ct = default)
    {
        var dialog = Gtk.FileDialog.New();
        dialog.SetTitle("Ordnerliste speichern");
        dialog.SetModal(true);
        dialog.SetInitialName(suggestedFileName);

        var window = GetActiveWindow();
        if (window is null) return null;

        try
        {
            var file = await dialog.SaveAsync(window);
            var path = file?.GetPath();
            if (path is null) return null;

            await File.WriteAllTextAsync(path, content, ct);
            return path;
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
