namespace GoBDify.Services;

/// <summary>
/// Linux/GTK-Implementierung von <see cref="IFolderPicker"/> über den nativen
/// <c>Gtk.FileDialog</c> im Ordner-Modus (GTK 4.10+). GTK kennt keine
/// persistente Zugriffsverwaltung wie die Windows-FutureAccessList — der
/// Pfadzugriff ist auf Linux ohnehin nicht sandboxed —, daher ist
/// <see cref="ForgetFolder"/> ein No-op.
/// </summary>
public sealed class GtkFolderPicker : IFolderPicker
{
    public async Task<string?> PickFolderAsync(string? initialPath)
    {
        var dialog = Gtk.FileDialog.New();
        dialog.SetTitle("Ordner auswählen");
        dialog.SetModal(true);

        var window = GetActiveWindow();
        if (window is null) return null;

        try
        {
            var folder = await dialog.SelectFolderAsync(window);
            return folder?.GetPath();
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

    public void ForgetFolder(string path) { /* GTK: keine persistente Zugriffsverwaltung nötig */ }

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
