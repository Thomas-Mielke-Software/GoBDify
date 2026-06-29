using GoBDify.Services;

namespace GoBDify.Views;

public partial class FlyoutContent : ContentView
{
    private readonly WorkspaceService _workspace;
    private string? _dragPath;
    private BoxView? _gapSpacer;
    private double _gapBase;
    private int _gapVersion;
    private int _scrollDir;
    private IDispatcherTimer? _dragTimer;

    public FlyoutContent()
    {
        InitializeComponent();
        _workspace = IPlatformApplication.Current!.Services.GetRequiredService<WorkspaceService>();
        _workspace.RecentFolders.CollectionChanged += (_, __) => Render();
        _workspace.CurrentFolderChanged += (_, __) => Render();
        Render();
    }

    private void Render()
    {
        RecentList.Children.Clear();
        var titles = FolderDisplay.DisambiguateTitles(_workspace.RecentFolders);
        foreach (var folder in _workspace.RecentFolders)
        {
            var path = folder;
            bool active = string.Equals(path, _workspace.CurrentFolder, StringComparison.OrdinalIgnoreCase);
            var label = new Label
            {
                Text = titles.TryGetValue(path, out var t) ? t : ShortName(path),
                FontSize = 14,
                TextColor = active ? Colors.White : Color.FromArgb("#D1D5DB"),
                FontAttributes = active ? FontAttributes.Bold : FontAttributes.None,
                LineBreakMode = LineBreakMode.TailTruncation
            };
            var sub = new Label
            {
                Text = path,
                FontSize = 10,
                TextColor = Color.FromArgb("#6B7280"),
                LineBreakMode = LineBreakMode.MiddleTruncation
            };
            var stack = new VerticalStackLayout { Spacing = 0, Children = { label, sub }, VerticalOptions = LayoutOptions.Center };

            var removeLabel = new Label
            {
                Text = "✕",
                FontSize = 14,
                TextColor = Color.FromArgb("#9CA3AF"),
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            };
            var removeBtn = new Border
            {
                WidthRequest = 24,
                HeightRequest = 24,
                StrokeThickness = 0,
                BackgroundColor = Colors.Transparent,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
                VerticalOptions = LayoutOptions.Center,
                Content = removeLabel
            };
            var removeTap = new TapGestureRecognizer();
            removeTap.Tapped += (_, __) =>
            {
                _workspace.RemoveRecent(path);
            };
            removeBtn.GestureRecognizers.Add(removeTap);
#if WINDOWS
            removeBtn.HandlerChanged += (s, _) =>
            {
                if (s is Border b && b.Handler?.PlatformView is Microsoft.UI.Xaml.UIElement ui)
                {
                    ui.PointerEntered += (_, _) => { b.BackgroundColor = Color.FromArgb("#374151"); removeLabel.TextColor = Colors.White; };
                    ui.PointerExited  += (_, _) => { b.BackgroundColor = Colors.Transparent; removeLabel.TextColor = Color.FromArgb("#9CA3AF"); };
                }
            };
#endif

            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection(
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)),
                ColumnSpacing = 4
            };
            grid.Add(stack, 0, 0);
            grid.Add(removeBtn, 1, 0);

            // Spacer innerhalb der Zeile: beim Drag-over wird seine Höhe animiert,
            // dadurch entsteht die Einfügelücke OBERHALB des Inhalts — der Cursor
            // bleibt aber über der Zeile (kein Leave/Enter-Flackern).
            var spacer = new BoxView { HeightRequest = 0, Color = Colors.Transparent };
            var border = new Border
            {
                Padding = new Thickness(10, 6, 6, 6),
                StrokeThickness = 0,
                BackgroundColor = active ? Color.FromArgb("#374151") : Colors.Transparent,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 6 },
                Content = new VerticalStackLayout { Spacing = 0, Children = { spacer, grid } }
            };
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, __) => { _workspace.CurrentFolder = path; Shell.Current.FlyoutIsPresented = false; Shell.Current.GoToAsync("//home"); };
            stack.GestureRecognizers.Add(tap);

            // Drag&Drop-Sortierung: jede Zeile ist Drag-Quelle und Drop-Ziel.
            var drag = new DragGestureRecognizer { CanDrag = true };
            drag.DragStarting += (_, __) =>
            {
                _dragPath = path;
                StartAutoScroll();
                // Verzögert ausblenden, sonst wird das Ghost-Bild vom noch
                // unsichtbaren Element erzeugt. Guard gegen sehr kurze Drags.
                Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(40), () =>
                {
                    if (_dragPath == path) border.IsVisible = false;
                });
            };
            // DropCompleted feuert auch bei Abbruch — Zeile wieder einblenden.
            drag.DropCompleted += (_, __) => { border.IsVisible = true; _dragPath = null; StopAutoScroll(); CloseGap(); };
            border.GestureRecognizers.Add(drag);

            var drop = new DropGestureRecognizer { AllowDrop = true };
            drop.DragOver += (_, e) =>
            {
                // Copy ist nötig, damit der Drop auslöst (None => Verbots-Cursor,
                // kein Drop). Den "Kopieren"-Eindruck korrigiert SetMoveVisual.
                e.AcceptedOperation = DataPackageOperation.Copy;
                DragDropHelper.SetMoveVisual(e);
                UpdateScrollDir(e);
                if (_dragPath == null || _dragPath == path) { CloseGap(); return; }
                OpenGap(spacer);
            };
            drop.DragLeave += (_, __) => { _scrollDir = 0; CloseGap(); };
            drop.Drop += (_, __) =>
            {
                CloseGap();
                if (_dragPath != null && _dragPath != path)
                    _workspace.MoveRecentBefore(_dragPath, path);
                _dragPath = null;
            };
            border.GestureRecognizers.Add(drop);

            RecentList.Children.Add(border);
        }

        // Drop-Zone unter dem letzten Eintrag, um ans Listenende zu verschieben.
        if (_workspace.RecentFolders.Count > 0)
            RecentList.Children.Add(BuildEndDropZone());
    }

    private View BuildEndDropZone()
    {
        var endSpacer = new BoxView { HeightRequest = EndZoneBase, Color = Colors.Transparent };
        var drop = new DropGestureRecognizer { AllowDrop = true };
        drop.DragOver += (_, e) =>
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            DragDropHelper.SetMoveVisual(e);
            UpdateScrollDir(e);
            if (_dragPath == null) { CloseGap(); return; }
            OpenGap(endSpacer, EndZoneBase);
        };
        drop.DragLeave += (_, __) => { _scrollDir = 0; CloseGap(); };
        drop.Drop += (_, __) =>
        {
            CloseGap();
            if (_dragPath != null) _workspace.MoveRecentToEnd(_dragPath);
            _dragPath = null;
        };
        endSpacer.GestureRecognizers.Add(drop);
        return endSpacer;
    }

    // Höhe der eingeblendeten Lücke beim Drag-over (ungefähre Zeilenhöhe).
    private const double DropGap = 40;
    // Dauerhaft sichtbare (transparente) Trefferfläche der End-Drop-Zone.
    private const double EndZoneBase = 24;

    private void OpenGap(BoxView spacer, double baseHeight = 0)
    {
        if (ReferenceEquals(_gapSpacer, spacer)) return;
        CloseGap();
        _gapSpacer = spacer;
        _gapBase = baseHeight;
        int v = ++_gapVersion;
        // Leicht verzögert öffnen: schnelles Drüberziehen löst keine Lücke aus.
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(60), () =>
        {
            if (v != _gapVersion || !ReferenceEquals(_gapSpacer, spacer)) return;
            AnimateGap(spacer, baseHeight + DropGap, Easing.CubicOut, 180);
        });
    }

    private void CloseGap()
    {
        _gapVersion++;
        var spacer = _gapSpacer;
        var bas = _gapBase;
        _gapSpacer = null;
        if (spacer != null) AnimateGap(spacer, bas, Easing.CubicIn, 140);
    }

    private void StartAutoScroll()
    {
        _dragTimer ??= CreateAutoScrollTimer();
        _scrollDir = 0;
        if (!_dragTimer.IsRunning) _dragTimer.Start();
    }

    private void StopAutoScroll()
    {
        _scrollDir = 0;
        _dragTimer?.Stop();
    }

    private IDispatcherTimer CreateAutoScrollTimer()
    {
        var t = Dispatcher.CreateTimer();
        t.Interval = TimeSpan.FromMilliseconds(50);
        t.Tick += (_, __) =>
        {
            if (_scrollDir == 0) return;
            double max = Math.Max(0, RecentScroll.ContentSize.Height - RecentScroll.Height);
            double target = Math.Clamp(RecentScroll.ScrollY + _scrollDir * 24, 0, max);
            if (Math.Abs(target - RecentScroll.ScrollY) > 0.5)
                _ = RecentScroll.ScrollToAsync(0, target, false);
        };
        return t;
    }

    // Beim Drag-over die Scroll-Richtung anhand der Cursor-Nähe zum oberen/unteren
    // Rand des sichtbaren Listenbereichs setzen (Clamp stoppt an den Listenenden).
    private void UpdateScrollDir(DragEventArgs e)
    {
        const double edge = 36;
        if (e.GetPosition(RecentScroll) is Point p && RecentScroll.Height > 0)
            _scrollDir = p.Y < edge ? -1 : p.Y > RecentScroll.Height - edge ? 1 : 0;
        else
            _scrollDir = 0;
    }

    private static void AnimateGap(BoxView spacer, double to, Easing easing, uint length)
    {
        spacer.AbortAnimation("gap");
        double from = spacer.HeightRequest < 0 ? 0 : spacer.HeightRequest;
        new Animation(h => spacer.HeightRequest = h, from, to, easing).Commit(spacer, "gap", length: length);
    }

    private static string ShortName(string path)
    {
        try { return new DirectoryInfo(path).Name; } catch { return path; }
    }

    private async void OnAddFolderClicked(object sender, EventArgs e)
    {
        try
        {
            if (await _workspace.PickFolderAsync(_workspace.CurrentFolder))
            {
                Shell.Current.FlyoutIsPresented = false;
                await Shell.Current.GoToAsync("//home");
            }
        }
        catch (Exception ex)
        {
            await Application.Current!.MainPage!.DisplayAlert("Fehler", ex.Message, "OK");
        }
    }

    private async void OnExportFoldersClicked(object sender, EventArgs e)
    {
        var page = Application.Current!.MainPage!;
        try
        {
            if (_workspace.RecentFolders.Count == 0)
            {
                await page.DisplayAlert("Export", "Keine Ordner zum Exportieren vorhanden.", "OK");
                return;
            }
            var path = await _workspace.ExportRecentFoldersAsync();
            if (path != null)
            {
                Shell.Current.FlyoutIsPresented = false;
                await page.DisplayAlert("Export", $"Ordnerliste gespeichert:\n{path}", "OK");
            }
        }
        catch (Exception ex)
        {
            await page.DisplayAlert("Fehler", ex.Message, "OK");
        }
    }

    private async void OnImportFoldersClicked(object sender, EventArgs e)
    {
        var page = Application.Current!.MainPage!;
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
            if (result is null) return; // Datei-Dialog abgebrochen

            Shell.Current.FlyoutIsPresented = false;
            await page.DisplayAlert("Import", WorkspaceService.DescribeImportResult(result, mode.Value), "OK");
        }
        catch (Exception ex)
        {
            await page.DisplayAlert("Fehler", ex.Message, "OK");
        }
    }

    private async void OnSettingsClicked(object sender, EventArgs e)
    {
        Shell.Current.FlyoutIsPresented = false;
        await Shell.Current.GoToAsync("//settings");
    }
}
