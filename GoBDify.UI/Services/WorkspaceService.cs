using System.Collections.ObjectModel;
using System.Text;
using GoBDify.Core;

namespace GoBDify.Services;

public class WorkspaceService
{
    private readonly IFolderPicker _folderPicker;
    private readonly IFileSaver _fileSaver;
    private readonly IFileOpener _fileOpener;

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
        Settings.LastFolder = path;
        // Immer die ObservableCollection mitziehen — sonst sieht die Flyout-Liste
        // den neu hinzugefügten/umsortierten Ordner erst nach App-Neustart.
        SyncRecent();
        SaveSettings();
        CurrentFolderChanged?.Invoke(this, path);
    }

    public WorkspaceService(IFolderPicker folderPicker, IFileSaver fileSaver, IFileOpener fileOpener)
    {
        _folderPicker = folderPicker;
        _fileSaver = fileSaver;
        _fileOpener = fileOpener;
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

    /// <summary>
    /// Exportiert die aktuelle Ordnerliste über den plattformeigenen Speichern-Dialog
    /// als einfache Textdatei (ein Pfad pro Zeile, mit Kommentarkopf). Plattform-
    /// neutral — der Dialog kommt aus der <see cref="IFileSaver"/>-Implementierung.
    /// </summary>
    /// <returns>Pfad der gespeicherten Datei, oder <c>null</c> bei Abbruch.</returns>
    public Task<string?> ExportRecentFoldersAsync()
    {
        var name = $"gobdify-ordnerliste-{DateTime.Now:yyyy-MM-dd}.txt";
        return _fileSaver.SaveTextAsync(name, BuildRecentFoldersText());
    }

    /// <summary>
    /// Öffnet eine Import-Datei über den plattformeigenen Datei-Dialog und übernimmt
    /// die darin gelisteten Ordner. Es werden ausschließlich Pfade übernommen, die
    /// auf der Platte tatsächlich existieren; nicht existierende werden in
    /// <see cref="ImportResult.MissingPaths"/> zurückgemeldet.
    /// </summary>
    /// <returns>Das Import-Ergebnis, oder <c>null</c> wenn der Dialog abgebrochen wurde.</returns>
    public async Task<ImportResult?> ImportRecentFoldersAsync(ImportMode mode)
    {
        var content = await _fileOpener.OpenTextAsync();
        if (content == null) return null;
        return ImportRecentFolders(content, mode);
    }

    /// <summary>
    /// Übernimmt die Ordnerpfade aus <paramref name="content"/> (Format wie von
    /// <see cref="BuildRecentFoldersText"/>): <see cref="ImportMode.Replace"/> ersetzt
    /// die bestehende Liste, <see cref="ImportMode.Merge"/> ergänzt sie. Nur existierende
    /// Ordner werden aufgenommen.
    /// </summary>
    public ImportResult ImportRecentFolders(string content, ImportMode mode)
    {
        var existing = new List<string>();
        var missing = new List<string>();
        foreach (var path in ParseFolderList(content))
        {
            if (Directory.Exists(path)) existing.Add(path);
            else missing.Add(path);
        }

        var before = Settings.LastFolder;
        if (mode == ImportMode.Replace)
            Settings.RecentFolders.Clear();

        int imported = 0;
        foreach (var path in existing)
        {
            if (!Settings.RecentFolders.Contains(path))
            {
                Settings.RecentFolders.Add(path);
                imported++;
            }
        }

        // Nach Replace kann der aktive Ordner aus der Liste gefallen sein.
        if (Settings.LastFolder != null && !Settings.RecentFolders.Contains(Settings.LastFolder))
            Settings.LastFolder = null;

        SyncRecent();
        SaveSettings();
        if (Settings.LastFolder != before)
            CurrentFolderChanged?.Invoke(this, Settings.LastFolder);

        return new ImportResult(imported, missing.Count, missing);
    }

    /// <summary>Beschreibt das Import-Ergebnis als zusammengefassten Meldungstext.</summary>
    public static string DescribeImportResult(ImportResult r, ImportMode mode)
    {
        var verb = mode == ImportMode.Replace ? "ersetzt" : "zusammengeführt";
        var sb = new StringBuilder();
        sb.Append(r.Imported == 1
            ? $"1 Ordner importiert (Liste {verb})."
            : $"{r.Imported} Ordner importiert (Liste {verb}).");
        if (r.Skipped > 0)
        {
            sb.Append("\n\n");
            sb.Append(r.Skipped == 1
                ? "1 Ordner wurde übersprungen, weil er nicht (mehr) existiert:\n"
                : $"{r.Skipped} Ordner wurden übersprungen, weil sie nicht (mehr) existieren:\n");
            foreach (var p in r.MissingPaths) sb.Append("• ").Append(p).Append('\n');
        }
        return sb.ToString();
    }

    private static List<string> ParseFolderList(string content)
    {
        var list = new List<string>();
        foreach (var raw in content.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            list.Add(line);
        }
        return list;
    }

    /// <summary>Baut den Textinhalt der Ordnerliste (LF-Zeilenenden, UTF-8).</summary>
    public string BuildRecentFoldersText()
    {
        var sb = new StringBuilder();
        sb.Append("# GoBDify – Ordnerliste\n");
        sb.Append($"# Exportiert: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}\n");
        sb.Append($"# {RecentFolders.Count} Ordner\n");
        sb.Append('\n');
        foreach (var f in RecentFolders) sb.Append(f).Append('\n');
        return sb.ToString();
    }

    private void SyncRecent()
    {
        RecentFolders.Clear();
        foreach (var f in Settings.RecentFolders) RecentFolders.Add(f);
    }
}

/// <summary>Wie eine importierte Ordnerliste mit der bestehenden verrechnet wird.</summary>
public enum ImportMode
{
    /// <summary>Bestehende Liste verwerfen und durch die Import-Liste ersetzen.</summary>
    Replace,
    /// <summary>Import-Liste an die bestehende anhängen (Duplikate werden ausgelassen).</summary>
    Merge
}

/// <summary>Ergebnis eines Ordnerlisten-Imports.</summary>
/// <param name="Imported">Anzahl tatsächlich übernommener Ordner.</param>
/// <param name="Skipped">Anzahl übersprungener (nicht existierender) Ordner.</param>
/// <param name="MissingPaths">Die übersprungenen Pfade.</param>
public sealed record ImportResult(int Imported, int Skipped, IReadOnlyList<string> MissingPaths);
