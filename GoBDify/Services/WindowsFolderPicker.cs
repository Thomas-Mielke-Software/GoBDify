using CommunityToolkit.Maui.Storage;

namespace GoBDify.Services;

/// <summary>
/// Windows-Implementierung von <see cref="IFolderPicker"/>: CommunityToolkit-
/// FolderPicker plus Registrierung der Auswahl in der Windows-FutureAccessList
/// (siehe <see cref="WindowsFolderAccess"/>), damit der Zugriff über
/// App-Neustarts hinweg erhalten bleibt.
/// </summary>
public sealed class WindowsFolderPicker : IFolderPicker
{
    public async Task<string?> PickFolderAsync(string? initialPath)
    {
        var result = await FolderPicker.PickAsync(initialPath ?? string.Empty, default);
        var path = result?.Folder?.Path;
        if (path != null)
            await WindowsFolderAccess.RegisterAsync(path);
        return path;
    }

    public void ForgetFolder(string path) => WindowsFolderAccess.Forget(path);
}
