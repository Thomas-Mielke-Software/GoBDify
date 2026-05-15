using System.Collections.ObjectModel;
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
                if (Settings.RecentFolders.Count > 12)
                    Settings.RecentFolders.RemoveAt(Settings.RecentFolders.Count - 1);
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
            SyncRecent();
            SaveSettings();
        }
    }

    public void SaveSettings() => AppSettingsStore.Save(Settings);

    private void SyncRecent()
    {
        RecentFolders.Clear();
        foreach (var f in Settings.RecentFolders) RecentFolders.Add(f);
    }
}
