using GoBDify.Core;
using GoBDify.Services;
using Microsoft.Maui.Controls.Shapes;

namespace GoBDify.Views;

public partial class HomePage : ContentPage
{
    private readonly WorkspaceService _workspace;
    private bool _running;

    public HomePage()
    {
        InitializeComponent();
        _workspace = IPlatformApplication.Current!.Services.GetRequiredService<WorkspaceService>();
        _workspace.CurrentFolderChanged += async (_, __) =>
        {
            UpdateHeader();
            await RunAuditAsync();
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        UpdateHeader();
        if (string.IsNullOrEmpty(_workspace.CurrentFolder))
        {
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

    private async Task RunAuditAsync()
    {
        if (_running || string.IsNullOrEmpty(_workspace.CurrentFolder)) return;
        _running = true;
        SetBusy(true);
        StatusLabel.Text = "Audit läuft …";
        try
        {
            var progress = new Progress<ProgressInfo>(p => Progress.Progress = p.Fraction);
            var report = await _workspace.Processor.AuditAsync(_workspace.CurrentFolder!, progress);
            RenderReport(report, newTimestamp: null);
            StatusLabel.Text = report.Error ?? $"Audit abgeschlossen — {Timestamps(report.Chain.Count)}, {Files(report.NewFiles.Count)} neu.";
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Fehler: {ex.Message}";
        }
        finally
        {
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
        SetBusy(true);
        StatusLabel.Text = _workspace.Settings.SwissMode
            ? "Audit läuft, Schweiz-Modus (drei Timestamp-Services) …"
            : "Audit läuft und neue Dateien werden getimestampt …";
        try
        {
            var progress = new Progress<ProgressInfo>(p => Progress.Progress = p.Fraction);
            var result = await _workspace.Processor.AuditAndTimestampAsync(_workspace.CurrentFolder!, _workspace.Settings, progress);
            RenderReport(result.Audit, result);
            if (result.NewChainNumber.HasValue)
                StatusLabel.Text = $"Neuer Timestamp #{result.NewChainNumber:D5} erstellt mit {result.NewTimestamps!.Count} Signatur(en).";
            else if (result.Audit.Error != null)
                StatusLabel.Text = result.Audit.Error;
            else
                StatusLabel.Text = "Keine neuen Dateien — kein neuer Timestamp nötig.";
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Fehler: {ex.Message}";
        }
        finally
        {
            _running = false;
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        Progress.IsVisible = busy;
        Progress.Progress = 0;
        AuditBtn.IsEnabled = !busy && _workspace.CurrentFolder != null;
        TimestampBtn.IsEnabled = !busy && _workspace.CurrentFolder != null;
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

        // chain entries that contain any modification/missing/bad-ts
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

        bool problems = modified > 0 || missing > 0 || badTs > 0;
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
                ? $"Bisherige {Files(totalFiles)} intakt, neue Dateien mit {Signed(signed)} beglaubigt (Schweiz-Modus)."
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

    private void RenderReport(AuditReport report, ChainResult? newTimestamp)
    {
        RenderSummary(report, newTimestamp);
        ChainContainer.Children.Clear();
        foreach (var entry in report.Chain)
            ChainContainer.Children.Add(BuildChainCard(entry, highlight: false));

        if (newTimestamp?.NewChainNumber.HasValue == true)
        {
            // build a synthetic entry visualization for the just-created timestamp
            var freshEntry = new ChainEntry(
                newTimestamp.NewChainNumber.Value,
                $"timestamp{newTimestamp.NewChainNumber:D5}.sha256",
                report.NewFiles.Select(f => new FileEntry(f, "", null, FileVerificationStatus.New)).ToList(),
                newTimestamp.NewTimestamps ?? new List<TimestampTokenInfo>());
            ChainContainer.Children.Add(BuildChainCard(freshEntry, highlight: true));
        }

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
        else
        {
            NewFilesBorder.IsVisible = false;
        }

        FooterLabel.Text = report.Chain.Count == 0
            ? "Noch keine Hash-Kette in diesem Ordner."
            : $"{Timestamps(report.Chain.Count)}  •  {Files(report.Chain.Sum(c => c.Files.Count))} in der Kette";

        ScrollToEndSafe();
    }

    private void ScrollToEndSafe()
    {
        // einmal sofort (für nachfolgende Renderings nach dem ersten Layout)
        Dispatcher.Dispatch(async () =>
        {
            try { await MainScroll.ScrollToAsync(ScrollAnchor, ScrollToPosition.End, animated: true); } catch { }
        });
        // und nochmal verzögert, damit es auch direkt beim ersten Anzeigen der Seite klappt,
        // wenn ContentSize beim Dispatch noch 0 ist
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(250), async () =>
        {
            try { await MainScroll.ScrollToAsync(ScrollAnchor, ScrollToPosition.End, animated: true); } catch { }
        });
    }

    private View BuildChainCard(ChainEntry entry, bool highlight)
    {
        bool allOk = entry.Files.All(f => f.Status is FileVerificationStatus.Ok or FileVerificationStatus.New)
                     && entry.Timestamps.All(t => t.Status == TimestampStatus.Valid);
        var accent = highlight ? "#10B981" : (allOk ? "#5d76dd" : "#DC2626");

        var header = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)), ColumnSpacing = 10 };
        header.Add(new Label { Text = allOk ? "✓" : "!", FontSize = 18, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb(accent), VerticalOptions = LayoutOptions.Center }, 0, 0);

        var titleStack = new VerticalStackLayout { Spacing = 1 };
        titleStack.Add(new Label { Text = entry.Sha256FileName, FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#111827") });
        var firstTs = entry.Timestamps.FirstOrDefault();
        if (firstTs?.Timestamp != null)
            titleStack.Add(new Label { Text = firstTs.Timestamp.Value.ToLocalTime().ToString("dd.MM.yyyy HH:mm"), FontSize = 11, TextColor = Color.FromArgb("#6B7280") });
        header.Add(titleStack, 1, 0);

        // TSA badges
        var badges = new HorizontalStackLayout { Spacing = 4, VerticalOptions = LayoutOptions.Center };
        foreach (var t in entry.Timestamps)
        {
            var bg = t.Status == TimestampStatus.Valid ? "#DBEAFE" : "#FEE2E2";
            var fg = t.Status == TimestampStatus.Valid ? "#1E40AF" : "#991B1B";
            var label = !string.IsNullOrEmpty(t.TsaFileSuffix) ? t.TsaFileSuffix.ToUpper() : "TS";
            badges.Add(new Border
            {
                BackgroundColor = Color.FromArgb(bg),
                StrokeThickness = 0,
                Padding = new Thickness(6, 2),
                StrokeShape = new RoundRectangle { CornerRadius = 4 },
                Content = new Label { Text = label, FontSize = 10, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb(fg) }
            });
        }
        header.Add(badges, 2, 0);

        var fileList = new VerticalStackLayout { Spacing = 3, Padding = new Thickness(28, 6, 0, 0) };
        foreach (var f in entry.Files)
        {
            string icon = f.Status switch
            {
                FileVerificationStatus.Ok => "✓",
                FileVerificationStatus.New => "+",
                FileVerificationStatus.Modified => "⚠",
                FileVerificationStatus.Missing => "✗",
                _ => "•"
            };
            string color = f.Status switch
            {
                FileVerificationStatus.Ok => "#059669",
                FileVerificationStatus.New => "#10B981",
                FileVerificationStatus.Modified => "#DC2626",
                FileVerificationStatus.Missing => "#DC2626",
                _ => "#6B7280"
            };
            var row = new HorizontalStackLayout { Spacing = 8 };
            row.Add(new Label { Text = icon, FontSize = 13, TextColor = Color.FromArgb(color), WidthRequest = 14 });
            row.Add(new Label { Text = f.FileName, FontSize = 13, TextColor = Color.FromArgb("#1F2937") });
            if (f.Status == FileVerificationStatus.Modified)
                row.Add(new Label { Text = "(verändert)", FontSize = 11, TextColor = Color.FromArgb("#DC2626") });
            else if (f.Status == FileVerificationStatus.Missing)
                row.Add(new Label { Text = "(fehlt)", FontSize = 11, TextColor = Color.FromArgb("#DC2626") });
            fileList.Add(row);
        }

        // detailed timestamp info
        if (entry.Timestamps.Count > 1 || entry.Timestamps.Any(t => t.Status != TimestampStatus.Valid))
        {
            foreach (var t in entry.Timestamps)
            {
                var line = $"  {(string.IsNullOrEmpty(t.TsaFileSuffix) ? "" : t.TsaFileSuffix.ToUpper() + ": ")}{(t.IssuerName ?? t.TstFileName)} — {(t.Status == TimestampStatus.Valid ? "gültig" : (t.Error ?? "ungültig"))}";
                fileList.Add(new Label { Text = line, FontSize = 10, TextColor = Color.FromArgb("#6B7280") });
            }
        }

        var inner = new VerticalStackLayout { Spacing = 0 };
        inner.Add(header);
        inner.Add(fileList);

        return new Border
        {
            BackgroundColor = Colors.White,
            Stroke = Color.FromArgb(highlight ? accent : "#E5E7EB"),
            StrokeThickness = highlight ? 2 : 1,
            Padding = new Thickness(14, 12),
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            Content = inner
        };
    }
}
