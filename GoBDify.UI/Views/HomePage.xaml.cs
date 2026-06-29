using GoBDify.Core;
using GoBDify.Services;
using Microsoft.Maui.Controls.Shapes;

namespace GoBDify.Views;

public partial class HomePage : ContentPage
{
    private readonly WorkspaceService _workspace;
    private bool _running;
    private CancellationTokenSource? _cts;

    // live UI controllers keyed by ChainIndex from events
    private readonly Dictionary<int, ChainCard> _cards = new();

    // last started but not yet completed file (chainIndex, fileName) — Ziel der Abbruch-Markierung
    private (int chainIndex, string fileName)? _activeFile;

    public HomePage()
    {
        InitializeComponent();
        _workspace = IPlatformApplication.Current!.Services.GetRequiredService<WorkspaceService>();
        _workspace.CurrentFolderChanged += async (_, __) =>
        {
            UpdateHeader();
            await RunAuditAsync();
        };

        // Auf Linux/GTK liegt die Ordnerverwaltung im linken Flyout (eigener
        // FolderFlyoutShellHandler im Linux-Head). Die seiten-interne FolderBar
        // bleibt daher ausgeblendet; Windows behält seinen Flyout unverändert.
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        UpdateHeader();
        if (string.IsNullOrEmpty(_workspace.CurrentFolder))
        {
            // Ohne Ordner den Flyout zur Auswahl öffnen (auf Linux enthält er jetzt
            // die Ordnerverwaltung, auf Windows den FlyoutContent).
            Shell.Current.FlyoutIsPresented = true;
            return;
        }
        if (!_running && ChainContainer.Children.Count == 0)
            await RunAuditAsync();
    }

    private void UpdateHeader()
    {
        var folder = _workspace.CurrentFolder;
        if (string.IsNullOrEmpty(folder))
        {
            Title = "GoBDify";
            FolderPathLabel.Text = "Kein Ordner ausgewählt";
            AuditBtn.IsEnabled = false;
            TimestampBtn.IsEnabled = false;
        }
        else
        {
            Title = new DirectoryInfo(folder).Name;
            FolderPathLabel.Text = folder;
            AuditBtn.IsEnabled = !_running;
            TimestampBtn.IsEnabled = !_running;
        }
    }

    private async void OnAuditClicked(object sender, EventArgs e) => await RunAuditAsync();
    private async void OnTimestampClicked(object sender, EventArgs e) => await RunTimestampAsync();
    private void OnCancelClicked(object sender, EventArgs e)
    {
        _cts?.Cancel();
        CancelBtn.IsEnabled = false;
        CancelBtn.Text = "Wird abgebrochen …";
    }

    private async Task RunAuditAsync()
    {
        if (_running || string.IsNullOrEmpty(_workspace.CurrentFolder)) return;
        _running = true;
        _cts = new CancellationTokenSource();
        SetBusy(true);
        ResetUi();
        StatusLabel.Text = "Audit läuft …";
        try
        {
            var progress = new Progress<ProgressInfo>(p => Progress.Progress = p.Fraction);
            var events = new Progress<ChainEvent>(OnChainEvent);
            var report = await _workspace.Processor.AuditAsync(_workspace.CurrentFolder!, progress, events, _cts.Token);
            FinalizeReport(report, newTimestamp: null);
        }
        catch (OperationCanceledException)
        {
            HandleCancelled();
        }
        catch (UnauthorizedAccessException)
        {
            HandlePermissionDenied();
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Fehler: {ex.Message}";
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            _running = false;
            SetBusy(false);
        }
    }

    private async Task RunTimestampAsync()
    {
        if (_running || string.IsNullOrEmpty(_workspace.CurrentFolder)) return;
        var validation = _workspace.Settings.Validate();
        if (validation != null)
        {
            await DisplayAlert("Konfiguration", validation, "Zu den Einstellungen");
            await Shell.Current.GoToAsync("//settings");
            return;
        }
        _running = true;
        _cts = new CancellationTokenSource();
        SetBusy(true);
        ResetUi();
        StatusLabel.Text = _workspace.Settings.ParanoiaMode
            ? "Audit läuft, Paranoia-Modus (drei Timestamp-Services) …"
            : "Audit läuft und neue Dateien werden getimestampt …";
        try
        {
            var progress = new Progress<ProgressInfo>(p => Progress.Progress = p.Fraction);
            var events = new Progress<ChainEvent>(OnChainEvent);
            var result = await _workspace.Processor.AuditAndTimestampAsync(_workspace.CurrentFolder!, _workspace.Settings, progress, events, _cts.Token);
            FinalizeReport(result.Audit, result);
        }
        catch (OperationCanceledException)
        {
            HandleCancelled();
        }
        catch (UnauthorizedAccessException)
        {
            HandlePermissionDenied();
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Fehler: {ex.Message}";
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            _running = false;
            SetBusy(false);
        }
    }

    private void HandlePermissionDenied()
    {
        _activeFile = null;
        PermissionDetail.Text =
            "Die App läuft in einer Sandbox (Microsoft Store oder MSIX) und hat noch keinen " +
            "registrierten Zugriff auf diesen Ordner. Ursache nach Neuinstallation oder " +
            "Settings-Backup. Klicke 'Zugriff freigeben' und bestätige den Ordner im Dialog — " +
            "das erteilt Windows die Berechtigung dauerhaft.";
        PermissionBorder.IsVisible = true;
        SummaryBorder.IsVisible = false;
        NewFilesBorder.IsVisible = false;
        StatusLabel.Text = "";

        Dispatcher.Dispatch(async () =>
        {
            try { await MainScroll.ScrollToAsync(0, 0, animated: true); } catch { }
        });
    }

    private async void OnGrantAccessClicked(object sender, EventArgs e)
    {
        try
        {
            var current = _workspace.CurrentFolder;
            if (await _workspace.PickFolderAsync(current))
            {
                // CurrentFolderChanged-Handler triggert den Audit automatisch.
                // Falls der User denselben Pfad bestätigt, kein Auto-Trigger — manuell:
                if (current == _workspace.CurrentFolder)
                    await RunAuditAsync();
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Fehler", ex.Message, "OK");
        }
    }

    private void HandleCancelled()
    {
        // markiere die aktuell im Hashing befindliche Datei
        if (_activeFile is { } act && _cards.TryGetValue(act.chainIndex, out var card))
            card.MarkFileCancelled(act.fileName);
        _activeFile = null;

        StatusLabel.Text = "Vom Benutzer abgebrochen.";
        StatusLabel.TextColor = Color.FromArgb("#DC2626");
        SummaryBorder.IsVisible = false;
        NewFilesBorder.IsVisible = false;
        FooterLabel.Text = "";
    }

    private void ResetUi()
    {
        _cards.Clear();
        _activeFile = null;
        ChainContainer.Children.Clear();
        SummaryBorder.IsVisible = false;
        NewFilesBorder.IsVisible = false;
        PermissionBorder.IsVisible = false;
        StatusLabel.Text = "";
        StatusLabel.TextColor = Color.FromArgb("#6B7280");
        FooterLabel.Text = "";
    }

    private void SetBusy(bool busy)
    {
        Progress.IsVisible = busy;
        Progress.Progress = 0;
        AuditBtn.IsEnabled = !busy && _workspace.CurrentFolder != null;
        TimestampBtn.IsEnabled = !busy && _workspace.CurrentFolder != null;
        CancelBtn.IsVisible = busy;
        CancelBtn.IsEnabled = busy;
        CancelBtn.Text = "Abbrechen";
        if (!busy)
            StatusLabel.TextColor = Color.FromArgb("#6B7280");
    }

    private void OnChainEvent(ChainEvent ev)
    {
        switch (ev)
        {
            case ChainDiscovered cd:
            {
                var card = new ChainCard(cd.ChainNumber, cd.Sha256FileName, cd.FileNames, cd.Timestamps, highlight: false);
                _cards[cd.ChainIndex] = card;
                ChainContainer.Children.Add(card.Root);
                ScrollTo(card.Root);
                break;
            }
            case TimestampVerified tv:
            {
                if (_cards.TryGetValue(tv.ChainIndex, out var card))
                    card.UpdateTimestamp(tv);
                break;
            }
            case FileHashStarted fs:
            {
                _activeFile = (fs.ChainIndex, fs.FileName);
                if (_cards.TryGetValue(fs.ChainIndex, out var card))
                {
                    var row = card.MarkFileBusy(fs.FileName);
                    if (row != null) ScrollTo(row);
                }
                break;
            }
            case FileHashCompleted fc:
            {
                _activeFile = null;
                if (_cards.TryGetValue(fc.ChainIndex, out var card))
                    card.SetFileStatus(fc.FileName, fc.Status);
                break;
            }
            case NewChainStarting nc:
            {
                var card = new ChainCard(nc.ChainNumber, nc.Sha256FileName, nc.FileNames,
                    Array.Empty<(string, string)>(), highlight: true);
                _cards[nc.ChainIndex] = card;
                ChainContainer.Children.Add(card.Root);
                ScrollTo(card.Root);
                // hide the "Neue Dateien"-Box since they are now in the fresh card
                NewFilesBorder.IsVisible = false;
                break;
            }
            case NewFileHashStarted nfs:
            {
                _activeFile = (nfs.ChainIndex, nfs.FileName);
                if (_cards.TryGetValue(nfs.ChainIndex, out var card))
                {
                    var row = card.MarkFileBusy(nfs.FileName);
                    if (row != null) ScrollTo(row);
                }
                break;
            }
            case NewFileHashCompleted nfc:
            {
                _activeFile = null;
                if (_cards.TryGetValue(nfc.ChainIndex, out var card))
                    card.SetFileStatus(nfc.FileName, FileVerificationStatus.New);
                break;
            }
            case NewTimestampRequested ntr:
            {
                if (_cards.TryGetValue(ntr.ChainIndex, out var card))
                    card.AddTimestampBadge(ntr.TstFileName, ntr.Timestamp, ntr.TsaName);
                break;
            }
            case AuditCompleted ac:
            {
                // handled in FinalizeReport, but we can already render the "new files" box if no NewChainStarting follows
                break;
            }
        }
    }

    private void FinalizeReport(AuditReport report, ChainResult? newTimestamp)
    {
        bool emptyAudit = report.Chain.Count == 0 && newTimestamp?.NewChainNumber == null;

        RenderSummary(report, newTimestamp);

        if (newTimestamp == null && report.NewFiles.Count > 0)
        {
            NewFilesBorder.IsVisible = true;
            NewFilesHeadline.Text = report.NewFiles.Count == 1
                ? "Neue, noch nicht getimestampte Datei"
                : "Neue, noch nicht getimestampte Dateien";
            NewFilesList.Children.Clear();
            foreach (var f in report.NewFiles)
                NewFilesList.Children.Add(new Label { Text = "• " + f, FontSize = 13, TextColor = Color.FromArgb("#92400E") });
        }

        int totalAfter = report.Chain.Sum(c => c.Files.Count) + (newTimestamp?.NewChainNumber != null ? report.NewFiles.Count : 0);
        int chainsAfter = report.Chain.Count + (newTimestamp?.NewChainNumber != null ? 1 : 0);
        FooterLabel.Text = chainsAfter == 0
            ? "Noch keine Hash-Kette in diesem Ordner."
            : $"{Timestamps(chainsAfter)}  •  {Files(totalAfter)} in der Kette";

        if (emptyAudit)
        {
            // nichts zu auditen → keine Statusmeldung, Audit-Knopf bleibt deaktiviert, an den Anfang scrollen
            StatusLabel.Text = "";
            AuditBtn.IsEnabled = false;
            Dispatcher.Dispatch(async () =>
            {
                try { await MainScroll.ScrollToAsync(0, 0, animated: true); } catch { }
            });
            return;
        }

        if (report.Error != null)
            StatusLabel.Text = "";
        else if (newTimestamp?.NewChainNumber.HasValue == true)
            StatusLabel.Text = $"Neuer Timestamp #{newTimestamp.NewChainNumber:D5} erstellt mit {newTimestamp.NewTimestamps!.Count} Signatur(en).";
        else
            StatusLabel.Text = $"Audit abgeschlossen — {Timestamps(report.Chain.Count)}, {Files(report.NewFiles.Count)} neu.";

        ScrollToEndSafe();
    }

    private void ScrollTo(View target)
    {
        Dispatcher.Dispatch(async () =>
        {
            try { await MainScroll.ScrollToAsync(target, ScrollToPosition.MakeVisible, animated: true); } catch { }
        });
    }

    private void ScrollToEndSafe()
    {
        Dispatcher.Dispatch(async () =>
        {
            try { await MainScroll.ScrollToAsync(ScrollAnchor, ScrollToPosition.End, animated: true); } catch { }
        });
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(250), async () =>
        {
            try { await MainScroll.ScrollToAsync(ScrollAnchor, ScrollToPosition.End, animated: true); } catch { }
        });
    }

    private static string Files(int n) => n == 1 ? "1 Datei" : $"{n} Dateien";
    private static string Timestamps(int n) => n == 1 ? "1 Timestamp" : $"{n} Timestamps";
    private static string Signed(int n) => n == 1 ? "einer Signatur" : $"{n} Signaturen";

    private void RenderSummary(AuditReport report, ChainResult? newTimestamp)
    {
        int modified = report.Chain.Sum(c => c.Files.Count(f => f.Status == FileVerificationStatus.Modified));
        int missing  = report.Chain.Sum(c => c.Files.Count(f => f.Status == FileVerificationStatus.Missing));
        int badTs    = report.Chain.Sum(c => c.Timestamps.Count(t => t.Status != TimestampStatus.Valid));
        int totalFiles = report.Chain.Sum(c => c.Files.Count);
        int newCount = report.NewFiles.Count;

        var affectedChains = report.Chain.Where(c =>
            c.Files.Any(f => f.Status == FileVerificationStatus.Modified || f.Status == FileVerificationStatus.Missing)
            || c.Timestamps.Any(t => t.Status != TimestampStatus.Valid)).ToList();

        SummaryBorder.IsVisible = true;

        if (report.Error != null)
        {
            SummaryBorder.BackgroundColor = Color.FromArgb("#FEE2E2");
            SummaryIcon.Text = "✕";
            SummaryIcon.TextColor = Color.FromArgb("#991B1B");
            SummaryHeadline.TextColor = Color.FromArgb("#991B1B");
            SummaryDetail.TextColor = Color.FromArgb("#991B1B");
            SummaryHeadline.Text = "Audit fehlgeschlagen";
            SummaryDetail.Text = report.Error;
            return;
        }

        bool problems = modified > 0 || missing > 0 || badTs > 0;

        // Erstbeglaubigung (Kette war leer, jetzt erster Timestamp erzeugt)
        if (report.Chain.Count == 0 && newTimestamp?.NewChainNumber.HasValue == true)
        {
            SummaryBorder.BackgroundColor = Color.FromArgb("#D1FAE5");
            SummaryIcon.Text = "✓";
            SummaryIcon.TextColor = Color.FromArgb("#065F46");
            SummaryHeadline.TextColor = Color.FromArgb("#065F46");
            SummaryDetail.TextColor = Color.FromArgb("#065F46");
            int signed = newTimestamp.NewTimestamps?.Count ?? 0;
            SummaryHeadline.Text = $"Erste Beglaubigung erstellt — {Files(newCount)} hinzugefügt";
            SummaryDetail.Text = signed > 1
                ? $"Hash-Kette begonnen, neue Dateien mit {Signed(signed)} beglaubigt (Paranoia-Modus)."
                : "Hash-Kette begonnen, neue Dateien mit einer Signatur beglaubigt.";
            return;
        }

        if (report.Chain.Count == 0)
        {
            SummaryBorder.BackgroundColor = Color.FromArgb("#FEF3C7");
            SummaryIcon.Text = "i";
            SummaryIcon.TextColor = Color.FromArgb("#92400E");
            SummaryHeadline.TextColor = Color.FromArgb("#92400E");
            SummaryDetail.TextColor = Color.FromArgb("#92400E");
            SummaryHeadline.Text = newCount > 0
                ? $"Noch keine Hash-Kette — {Files(newCount)} bereit zum Timestampen"
                : "Noch keine Hash-Kette und keine Dateien";
            SummaryDetail.Text = newCount > 0
                ? "Klicke 'Neuen Timestamp erstellen' um die Historie zu beginnen."
                : "Lege Dateien in den Ordner und erstelle einen Timestamp.";
            return;
        }

        if (problems)
        {
            SummaryBorder.BackgroundColor = Color.FromArgb("#FEE2E2");
            SummaryIcon.Text = "⚠";
            SummaryIcon.TextColor = Color.FromArgb("#991B1B");
            SummaryHeadline.TextColor = Color.FromArgb("#991B1B");
            SummaryDetail.TextColor = Color.FromArgb("#7F1D1D");

            var parts = new List<string>();
            if (modified > 0) parts.Add(modified == 1 ? "1 Datei verändert" : $"{modified} Dateien verändert");
            if (missing  > 0) parts.Add(missing  == 1 ? "1 Datei fehlt"     : $"{missing} Dateien fehlen");
            if (badTs    > 0) parts.Add(badTs    == 1 ? "1 ungültiger Timestamp" : $"{badTs} ungültige Timestamps");
            SummaryHeadline.Text = "Historie verletzt — " + string.Join(", ", parts);

            string detail;
            if (affectedChains.Count == 1)
                detail = $"Veränderungen seit Beglaubigung durch {affectedChains[0].Sha256FileName} wurden erkannt.";
            else if (affectedChains.Count > 1)
                detail = "Veränderungen seit Beglaubigung wurden erkannt.";
            else
                detail = "Veränderungen erkannt.";
            SummaryDetail.Text = $"{Files(totalFiles)} in {Timestamps(report.Chain.Count)} geprüft. " + detail;
        }
        else if (newTimestamp?.NewChainNumber.HasValue == true)
        {
            SummaryBorder.BackgroundColor = Color.FromArgb("#D1FAE5");
            SummaryIcon.Text = "✓";
            SummaryIcon.TextColor = Color.FromArgb("#065F46");
            SummaryHeadline.TextColor = Color.FromArgb("#065F46");
            SummaryDetail.TextColor = Color.FromArgb("#065F46");
            int signed = newTimestamp.NewTimestamps?.Count ?? 0;
            SummaryHeadline.Text = $"Neuer Timestamp erstellt — {Files(newCount)} hinzugefügt";
            SummaryDetail.Text = signed > 1
                ? $"Bisherige {Files(totalFiles)} intakt, neue Dateien mit {Signed(signed)} beglaubigt (Paranoia-Modus)."
                : $"Bisherige {Files(totalFiles)} intakt, neue Dateien mit einer Signatur beglaubigt.";
        }
        else if (newCount > 0)
        {
            SummaryBorder.BackgroundColor = Color.FromArgb("#DBEAFE");
            SummaryIcon.Text = "✓";
            SummaryIcon.TextColor = Color.FromArgb("#1E3A8A");
            SummaryHeadline.TextColor = Color.FromArgb("#1E3A8A");
            SummaryDetail.TextColor = Color.FromArgb("#1E3A8A");
            SummaryHeadline.Text = newCount == 1
                ? "Historie intakt — 1 neue Datei noch ohne Timestamp"
                : $"Historie intakt — {newCount} neue Dateien noch ohne Timestamp";
            SummaryDetail.Text = $"Alle {Files(totalFiles)} bisher beglaubigt und unverändert. Klicke 'Neuen Timestamp erstellen' um die neuen Dateien zu beglaubigen.";
        }
        else
        {
            SummaryBorder.BackgroundColor = Color.FromArgb("#D1FAE5");
            SummaryIcon.Text = "✓";
            SummaryIcon.TextColor = Color.FromArgb("#065F46");
            SummaryHeadline.TextColor = Color.FromArgb("#065F46");
            SummaryDetail.TextColor = Color.FromArgb("#065F46");
            SummaryHeadline.Text = $"Historie intakt — alle {Files(totalFiles)} unverändert";
            SummaryDetail.Text = $"{Timestamps(report.Chain.Count)} in der Kette, alle Signaturen gültig.";
        }
    }
}

/// <summary>Live UI-Controller für eine .sha256-Karte mit allen Datei-Zeilen.</summary>
internal sealed class ChainCard
{
    public Border Root { get; }
    private readonly HorizontalStackLayout _badges;
    private readonly Dictionary<string, ChainFileRow> _rows = new();
    private readonly bool _highlight;

    public ChainCard(int number, string sha256Name,
                     IReadOnlyList<string> fileNames,
                     IReadOnlyList<(string Suffix, string TstFileName)> timestamps,
                     bool highlight)
    {
        _highlight = highlight;
        string accent = highlight ? "#10B981" : "#9CA3AF"; // start neutral, finalize in summary

        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)),
            ColumnSpacing = 10
        };
        var titleStack = new VerticalStackLayout { Spacing = 1 };
        titleStack.Add(new Label { Text = sha256Name, FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#111827") });
        header.Add(titleStack, 1, 0);

        _badges = new HorizontalStackLayout { Spacing = 4, VerticalOptions = LayoutOptions.Center };
        // pre-create placeholder badges for the .tst files we found (state will be updated)
        foreach (var (suffix, _) in timestamps)
        {
            var badge = new Border
            {
                BackgroundColor = Color.FromArgb("#E5E7EB"),
                StrokeThickness = 0,
                Padding = new Thickness(6, 2),
                StrokeShape = new RoundRectangle { CornerRadius = 4 },
                Content = new Label
                {
                    Text = string.IsNullOrEmpty(suffix) ? "TS" : suffix.ToUpper(),
                    FontSize = 10, FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#374151")
                }
            };
            _badges.Add(badge);
        }
        header.Add(_badges, 2, 0);

        var fileList = new VerticalStackLayout { Spacing = 3, Padding = new Thickness(28, 6, 0, 0) };
        foreach (var f in fileNames)
        {
            var row = new ChainFileRow(f, highlight);
            _rows[f] = row;
            fileList.Add(row.Root);
        }

        var inner = new VerticalStackLayout { Spacing = 0 };
        inner.Add(header);
        inner.Add(fileList);

        Root = new Border
        {
            BackgroundColor = Colors.White,
            Stroke = Color.FromArgb(highlight ? "#10B981" : "#E5E7EB"),
            StrokeThickness = highlight ? 2 : 1,
            Padding = new Thickness(14, 12),
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            Content = inner
        };
    }

    public View? MarkFileBusy(string fileName)
    {
        if (_rows.TryGetValue(fileName, out var row))
        {
            row.SetBusy();
            return row.Root;
        }
        return null;
    }

    public void SetFileStatus(string fileName, FileVerificationStatus status)
    {
        if (_rows.TryGetValue(fileName, out var row))
            row.SetStatus(status);
    }

    public void MarkFileCancelled(string fileName)
    {
        if (_rows.TryGetValue(fileName, out var row))
            row.SetCancelled();
    }

    public void UpdateTimestamp(TimestampVerified tv)
    {
        // find matching badge by suffix; replace its colors
        int idx = -1;
        for (int i = 0; i < _badges.Children.Count; i++)
        {
            if (_badges.Children[i] is Border b && b.Content is Label l)
            {
                var key = string.IsNullOrEmpty(tv.TsaSuffix) ? "TS" : tv.TsaSuffix.ToUpper();
                if (l.Text == key) { idx = i; break; }
            }
        }
        if (idx < 0) return;
        var (bg, fg) = tv.Status == TimestampStatus.Valid ? ("#DBEAFE", "#1E40AF") : ("#FEE2E2", "#991B1B");
        if (_badges.Children[idx] is Border target)
        {
            target.BackgroundColor = Color.FromArgb(bg);
            if (target.Content is Label tl) tl.TextColor = Color.FromArgb(fg);
        }
    }

    public void AddTimestampBadge(string tstFileName, DateTimeOffset? when, string issuer)
    {
        // for new (just-created) timestamps without prediscovered suffix
        string label = "TS";
        // extract _x suffix if present
        var m = System.Text.RegularExpressions.Regex.Match(tstFileName, @"_(\w)\.sha256\.tst$");
        if (m.Success) label = m.Groups[1].Value.ToUpper();
        var badge = new Border
        {
            BackgroundColor = Color.FromArgb("#D1FAE5"),
            StrokeThickness = 0,
            Padding = new Thickness(6, 2),
            StrokeShape = new RoundRectangle { CornerRadius = 4 },
            Content = new Label { Text = label, FontSize = 10, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#065F46") }
        };
        _badges.Add(badge);
    }
}

internal sealed class ChainFileRow
{
    public HorizontalStackLayout Root { get; }
    private readonly Grid _iconSlot;
    private readonly Label _statusSuffix;
    private readonly bool _highlight;

    public ChainFileRow(string fileName, bool highlight)
    {
        _highlight = highlight;
        Root = new HorizontalStackLayout { Spacing = 8 };
        _iconSlot = new Grid { WidthRequest = 16, HeightRequest = 16, VerticalOptions = LayoutOptions.Center };
        Root.Add(_iconSlot);
        Root.Add(new Label { Text = fileName, FontSize = 13, TextColor = Color.FromArgb("#1F2937"), VerticalOptions = LayoutOptions.Center });
        _statusSuffix = new Label { Text = "", FontSize = 11, VerticalOptions = LayoutOptions.Center };
        Root.Add(_statusSuffix);
        // initial: small gray dot ("pending")
        SetPending();
    }

    public void SetPending()
    {
        _iconSlot.Children.Clear();
        _iconSlot.Children.Add(new Label
        {
            Text = "•",
            FontSize = 14,
            TextColor = Color.FromArgb("#D1D5DB"),
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center
        });
        _statusSuffix.Text = "";
    }

    public void SetBusy()
    {
        _iconSlot.Children.Clear();
        _iconSlot.Children.Add(new ActivityIndicator
        {
            IsRunning = true,
            Color = Color.FromArgb("#5d76dd"),
            WidthRequest = 14,
            HeightRequest = 14
        });
        _statusSuffix.Text = "wird gehasht …";
        _statusSuffix.TextColor = Color.FromArgb("#6B7280");
    }

    public void SetCancelled()
    {
        _iconSlot.Children.Clear();
        _iconSlot.Children.Add(new Label
        {
            Text = "✗",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#DC2626"),
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center
        });
        _statusSuffix.Text = "durch Benutzer abgebrochen";
        _statusSuffix.TextColor = Color.FromArgb("#DC2626");
    }

    public void SetStatus(FileVerificationStatus status)
    {
        _iconSlot.Children.Clear();
        (string icon, string color, string suffix) = status switch
        {
            FileVerificationStatus.Ok       => ("✓", "#059669", ""),
            FileVerificationStatus.New      => ("+", "#10B981", "neu"),
            FileVerificationStatus.Modified => ("⚠", "#DC2626", "verändert"),
            FileVerificationStatus.Missing  => ("✗", "#DC2626", "fehlt"),
            _                               => ("•", "#6B7280", "")
        };
        _iconSlot.Children.Add(new Label
        {
            Text = icon,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb(color),
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center
        });
        _statusSuffix.Text = string.IsNullOrEmpty(suffix) ? "" : $"({suffix})";
        _statusSuffix.TextColor = Color.FromArgb(color);
    }
}
