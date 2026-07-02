using GoBDify.Services;

namespace GoBDify.Views;

/// <summary>
/// Ordnerverwaltung für den linken Navigationsbereich (Shell-Flyout).
///
/// Auf dem GTK4-Backend rendert die Shell kein <c>FlyoutContentTemplate</c> — der
/// Flyout ist eine feste Struktur aus Text-Header, Item-ListBox und Text-Footer.
/// Der Linux-Head setzt diese View deshalb über einen eigenen ShellHandler per
/// <c>ToPlatform</c> in den Flyout-Bereich. Statt Drag&amp;Drop (vom GTK4-Backend
/// nicht unterstützt) wird hier mit ↑/↓-Knöpfen sortiert.
/// </summary>
public sealed class FlyoutFolderPanel : ContentView
{
    /// <summary>
    /// <see cref="VisualElement.AutomationId"/>-Marke der klickbaren Ordner-Zeilen.
    /// Der Linux-Head (<c>FolderFlyoutShellHandler</c>) erkennt darüber im GTK-Baum,
    /// welche Buttons linksbündig ausgerichtet werden sollen.
    /// </summary>
    public const string FolderRowName = "gobdify-folder-row";

    /// <summary>Wird nach jedem Neuaufbau der Ordnerliste ausgelöst (für plattform-
    /// spezifische Nachbearbeitung wie die linksbündige Button-Ausrichtung auf GTK).</summary>
    public event Action? ListRendered;

    private readonly WorkspaceService _workspace;
    private readonly VerticalStackLayout _list = new() { Spacing = 4 };

    public FlyoutFolderPanel(WorkspaceService workspace)
    {
        _workspace = workspace;

        var title = new Label
        {
            Text = "Arbeitsordner",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White
        };

        var pick = new Button
        {
            Text = "＋ Ordner wählen",
            FontSize = 13,
            HeightRequest = 36,
            CornerRadius = 6,
            Padding = new Thickness(12, 0),
            BackgroundColor = Color.FromArgb("#5d76dd"),
            TextColor = Colors.White
        };
        pick.Clicked += async (_, __) => await _workspace.PickFolderAsync();

        var export = SubtleButton("Export");
        export.Clicked += OnExportClicked;
        var import = SubtleButton("Import");
        import.Clicked += OnImportClicked;
        var ioRow = new Grid { ColumnDefinitions = Cols("*,*"), ColumnSpacing = 6 };
        ioRow.Add(export, 0, 0);
        ioRow.Add(import, 1, 0);

        var scroll = new ScrollView { Content = _list };

        // Grid statt VerticalStackLayout: die *-Zeile gibt dem ScrollView die ganze
        // restliche Flyout-Höhe (bis hinunter zur Item-ListBox), statt ihn nur auf
        // Inhaltshöhe zu begrenzen.
        var root = new Grid
        {
            Padding = new Thickness(12, 10),
            RowSpacing = 8,
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
            }
        };
        root.Add(title, 0, 0);
        root.Add(pick, 0, 1);
        root.Add(ioRow, 0, 2);
        root.Add(scroll, 0, 3);

        VerticalOptions = LayoutOptions.Fill;
        Content = root;

        _workspace.RecentFolders.CollectionChanged += (_, __) => Render();
        _workspace.CurrentFolderChanged += (_, __) => Render();
        Render();
    }

    private void Render()
    {
        _list.Children.Clear();

        if (_workspace.RecentFolders.Count == 0)
        {
            _list.Children.Add(new Label
            {
                Text = "Noch kein Ordner gewählt.",
                FontSize = 12,
                TextColor = Color.FromArgb("#9CA3AF")
            });
            ListRendered?.Invoke();
            return;
        }

        var titles = FolderDisplay.DisambiguateTitles(_workspace.RecentFolders);
        var folders = _workspace.RecentFolders.ToList();
        for (int i = 0; i < folders.Count; i++)
        {
            var path = folders[i];
            int index = i;
            bool active = string.Equals(path, _workspace.CurrentFolder, StringComparison.OrdinalIgnoreCase);

            var open = new Button
            {
                Text = titles.TryGetValue(path, out var t) ? t : ShortName(path),
                FontSize = 13,
                FontAttributes = active ? FontAttributes.Bold : FontAttributes.None,
                TextColor = active ? Colors.White : Color.FromArgb("#E5E7EB"),
                BackgroundColor = active ? Color.FromArgb("#5d76dd") : Color.FromArgb("#374151"),
                Padding = new Thickness(10, 6),
                CornerRadius = 6,
                HorizontalOptions = LayoutOptions.Fill,
                LineBreakMode = LineBreakMode.TailTruncation,
                // Markierung für den GTK-Handler (→ widget.SetName), der nur diese
                // Zeilen-Buttons linksbündig ausrichtet (Icon-/Aktionsknöpfe bleiben
                // zentriert). Tooltip zeigt den vollen Pfad, da die Sidebar schmal ist.
                AutomationId = FolderRowName
            };
            ToolTipProperties.SetText(open, path);
            open.Clicked += async (_, __) =>
            {
                _workspace.CurrentFolder = path;
                try { await Shell.Current.GoToAsync("//home"); } catch { /* schon auf Home */ }
            };

            var up = IconButton("↑", index > 0);
            up.Clicked += (_, __) => MoveUp(index);
            var down = IconButton("↓", index < folders.Count - 1);
            down.Clicked += (_, __) => MoveDown(index);
            var remove = IconButton("✕", true);
            remove.Clicked += (_, __) => _workspace.RemoveRecent(path);

            var row = new Grid
            {
                ColumnDefinitions = Cols("*,Auto,Auto,Auto"),
                ColumnSpacing = 4
            };
            row.Add(open, 0, 0);
            row.Add(up, 1, 0);
            row.Add(down, 2, 0);
            row.Add(remove, 3, 0);
            _list.Children.Add(row);
        }

        ListRendered?.Invoke();
    }

    private void MoveUp(int index)
    {
        var folders = _workspace.RecentFolders.ToList();
        if (index <= 0 || index >= folders.Count) return;
        _workspace.MoveRecentBefore(folders[index], folders[index - 1]);
    }

    private void MoveDown(int index)
    {
        var folders = _workspace.RecentFolders.ToList();
        if (index < 0 || index >= folders.Count - 1) return;
        if (index + 1 == folders.Count - 1)
            _workspace.MoveRecentToEnd(folders[index]);
        else
            _workspace.MoveRecentBefore(folders[index], folders[index + 2]);
    }

    private async void OnExportClicked(object? sender, EventArgs e)
    {
        var page = Shell.Current?.CurrentPage;
        try
        {
            if (_workspace.RecentFolders.Count == 0)
            {
                if (page != null) await page.DisplayAlert("Export", "Keine Ordner zum Exportieren vorhanden.", "OK");
                return;
            }
            var path = await _workspace.ExportRecentFoldersAsync();
            if (path != null && page != null)
                await page.DisplayAlert("Export", $"Ordnerliste gespeichert:\n{path}", "OK");
        }
        catch (Exception ex) when (page != null)
        {
            await page.DisplayAlert("Fehler", ex.Message, "OK");
        }
    }

    private async void OnImportClicked(object? sender, EventArgs e)
    {
        var page = Shell.Current?.CurrentPage;
        if (page == null) return;
        try
        {
            var choice = await page.DisplayActionSheet(
                "Ordnerliste importieren", "Abbrechen", null,
                "Bestehende ersetzen", "Zusammenführen");
            var mode = choice switch
            {
                "Bestehende ersetzen" => (ImportMode?)ImportMode.Replace,
                "Zusammenführen"      => ImportMode.Merge,
                _                     => null
            };
            if (mode is null) return;

            var result = await _workspace.ImportRecentFoldersAsync(mode.Value);
            if (result is null) return;

            await page.DisplayAlert("Import", WorkspaceService.DescribeImportResult(result, mode.Value), "OK");
        }
        catch (Exception ex)
        {
            await page.DisplayAlert("Fehler", ex.Message, "OK");
        }
    }

    private static Button SubtleButton(string text) => new()
    {
        Text = text,
        FontSize = 12,
        HeightRequest = 32,
        CornerRadius = 6,
        Padding = new Thickness(8, 0),
        BackgroundColor = Color.FromArgb("#374151"),
        TextColor = Color.FromArgb("#E5E7EB")
    };

    private static Button IconButton(string glyph, bool enabled) => new()
    {
        Text = glyph,
        FontSize = 12,
        WidthRequest = 30,
        HeightRequest = 30,
        Padding = 0,
        CornerRadius = 6,
        IsEnabled = enabled,
        BackgroundColor = Color.FromArgb("#374151"),
        TextColor = enabled ? Color.FromArgb("#E5E7EB") : Color.FromArgb("#6B7280")
    };

    private static ColumnDefinitionCollection Cols(string spec)
    {
        var defs = new ColumnDefinitionCollection();
        foreach (var part in spec.Split(','))
        {
            var p = part.Trim();
            defs.Add(new ColumnDefinition(
                p == "*" ? GridLength.Star :
                p == "Auto" ? GridLength.Auto :
                new GridLength(double.Parse(p))));
        }
        return defs;
    }

    private static string ShortName(string path)
    {
        try { return new DirectoryInfo(path).Name; } catch { return path; }
    }
}
