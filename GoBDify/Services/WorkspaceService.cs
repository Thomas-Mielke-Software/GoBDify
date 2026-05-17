using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Storage;
using GoBDify.Core;

namespace GoBDify.Services;

public class WorkspaceService
{
    public AppSettings Settings { get; private set; } = AppSettingsStore.Load();
    public ChainProcessor Processor { get; } = new();

    public ObservableCollection<string> RecentFolders { get; } = new();

    public event EventHandler<string?>? CurrentFolderChanged;

    public string? CurrentFolder
    {
        get => Settings.LastFolder;
        set
        {
            if (Settings.LastFolder == value) return;
            Settings.LastFolder = value;
            if (!string.IsNullOrEmpty(value))
            {
                Settings.RecentFolders.Remove(value);
                Settings.RecentFolders.Insert(0, value);
                SyncRecent();
            }
            SaveSettings();
            CurrentFolderChanged?.Invoke(this, value);
        }
    }

    public WorkspaceService()
    {
        SyncRecent();
    }

    public void RemoveRecent(string path)
    {
        if (Settings.RecentFolders.Remove(path))
        {
            if (Settings.LastFolder == path) Settings.LastFolder = null;
            WindowsFolderAccess.Forget(path);
            SyncRecent();
            SaveSettings();
        }
    }

    public void SaveSettings() => AppSettingsStore.Save(Settings);

    /// <summary>
    /// Öffnet den Folder-Picker (mit optionaler Pfad-Vorauswahl), registriert
    /// die Auswahl in der FutureAccessList und setzt sie als CurrentFolder.
    /// </summary>
    /// <returns>true wenn ein Ordner gewählt wurde, false bei Abbruch.</returns>
    public async Task<bool> PickFolderAsync(string? initialPath = null)
    {
        var result = await FolderPicker.PickAsync(initialPath ?? string.Empty, default);
        if (result?.Folder?.Path == null) return false;
        await WindowsFolderAccess.RegisterAsync(result.Folder.Path);
        CurrentFolder = result.Folder.Path;
        return true;
    }

    private void SyncRecent()
    {
        RecentFolders.Clear();
        foreach (var f in Settings.RecentFolders) RecentFolders.Add(f);
    }
}
