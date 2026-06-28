using System.Collections.ObjectModel;
using GoBDify.Core;

namespace GoBDify.Services;

public class WorkspaceService
{
    private readonly IFolderPicker _folderPicker;

    public AppSettings Settings { get; private set; } = AppSettingsStore.Load();
    public ChainProcessor Processor { get; } = new();

    public ObservableCollection<string> RecentFolders { get; } = new();

    public event EventHandler<string?>? CurrentFolderChanged;

    /// <summary>
    /// Wechselt das Arbeitsverzeichnis. Neue Pfade werden am Anfang der
    /// Recent-Liste eingefügt; bereits vorhandene Pfade behalten ihre Position
    /// (bloßes Wechseln darf die Listen-Sortierung nicht umwerfen).
    /// </summary>
    public string? CurrentFolder
    {
        get => Settings.LastFolder;
        set
        {
            if (Settings.LastFolder == value) return;
            Settings.LastFolder = value;
            if (!string.IsNullOrEmpty(value) && !Settings.RecentFolders.Contains(value))
            {
                Settings.RecentFolders.Insert(0, value);
                SyncRecent();
            }
            SaveSettings();
            CurrentFolderChanged?.Invoke(this, value);
        }
    }

    /// <summary>
    /// Wie der CurrentFolder-Setter, aber zieht den Pfad explizit an die
    /// Spitze der Recent-Liste — für Aktionen mit Subtext "ab jetzt der
    /// Hauptordner" (Picker-Pick, Zugriff freigeben).
    /// </summary>
    public void SetCurrentFolderToTop(string path)
    {
        Settings.RecentFolders.Remove(path);
        Settings.RecentFolders.Insert(0, path);
        if (Settings.LastFolder == path)
        {
            // CurrentFolder-Setter würde wegen Gleichheit nichts tun;
            // wir müssen Sync+Save selbst auslösen und das Event feuern.
            SyncRecent();
            SaveSettings();
            CurrentFolderChanged?.Invoke(this, path);
        }
        else
        {
            // ändert LastFolder; Setter macht Sync+Save+Event.
            // (Wir haben RecentFolders schon umsortiert; der Setter sieht
            // den Pfad in der Liste und rührt sie nicht mehr an.)
            CurrentFolder = path;
        }
    }

    public WorkspaceService(IFolderPicker folderPicker)
    {
        _folderPicker = folderPicker;
        SyncRecent();
    }

    public void RemoveRecent(string path)
    {
        if (Settings.RecentFolders.Remove(path))
        {
            if (Settings.LastFolder == path) Settings.LastFolder = null;
            _folderPicker.ForgetFolder(path);
            SyncRecent();
            SaveSettings();
        }
    }

    public void SaveSettings() => AppSettingsStore.Save(Settings);

    /// <summary>
    /// Öffnet den Folder-Picker (mit optionaler Pfad-Vorauswahl) und setzt die
    /// Auswahl als CurrentFolder. Plattformspezifische Zugriffspersistenz
    /// erledigt die <see cref="IFolderPicker"/>-Implementierung.
    /// </summary>
    /// <returns>true wenn ein Ordner gewählt wurde, false bei Abbruch.</returns>
    public async Task<bool> PickFolderAsync(string? initialPath = null)
    {
        var path = await _folderPicker.PickFolderAsync(initialPath);
        if (path == null) return false;
        SetCurrentFolderToTop(path);
        return true;
    }

    private void SyncRecent()
    {
        RecentFolders.Clear();
        foreach (var f in Settings.RecentFolders) RecentFolders.Add(f);
    }
}
