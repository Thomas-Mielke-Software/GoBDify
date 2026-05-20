using GoBDify.Core;
using GoBDify.Services;
using Microsoft.Maui.Controls.Shapes;

namespace GoBDify.Views;

public partial class SettingsPage : ContentPage
{
    private readonly WorkspaceService _workspace;
    private readonly Dictionary<string, CheckBox> _checkboxes = new();

    private const string RepoUrl = "https://github.com/Thomas-Mielke-Software/GoBDify";
    private const string LicenseUrl = "https://www.gnu.org/licenses/agpl-3.0.html";

    public SettingsPage()
    {
        InitializeComponent();
        _workspace = IPlatformApplication.Current!.Services.GetRequiredService<WorkspaceService>();
        BuildTsaList();
        ParanoiaSwitch.IsToggled = _workspace.Settings.ParanoiaMode;
        VersionLabel.Text = AppInfo.Current.VersionString;
        UpdateValidation();
    }

    private async void OnLicenseTapped(object sender, TappedEventArgs e)
    {
        try { await Launcher.OpenAsync(LicenseUrl); } catch { }
    }

    private async void OnRepoTapped(object sender, TappedEventArgs e)
    {
        try { await Launcher.OpenAsync(RepoUrl); } catch { }
    }

    private void BuildTsaList()
    {
        TsaList.Children.Clear();
        _checkboxes.Clear();
        foreach (var tsa in TimestampAuthorities.Defaults)
        {
            var cb = new CheckBox
            {
                IsChecked = _workspace.Settings.SelectedTsaIds.Contains(tsa.Id),
                Color = Color.FromArgb("#5d76dd"),
                VerticalOptions = LayoutOptions.Center
            };
            cb.CheckedChanged += (_, e) => OnTsaToggled(tsa.Id, e.Value);
            _checkboxes[tsa.Id] = cb;

            var grid = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)), ColumnSpacing = 8, Padding = new Thickness(0, 6) };
            grid.Add(cb, 0, 0);
            var info = new VerticalStackLayout { Spacing = 0, VerticalOptions = LayoutOptions.Center };
            info.Add(new Label { Text = tsa.Name, FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#111827") });
            info.Add(new Label { Text = tsa.Url, FontSize = 11, TextColor = Color.FromArgb("#6B7280") });
            grid.Add(info, 1, 0);
            TsaList.Children.Add(grid);
        }
    }

    private bool _suppressToggle;

    private void OnTsaToggled(string id, bool value)
    {
        if (_suppressToggle) return;

        var list = _workspace.Settings.SelectedTsaIds;
        if (value)
        {
            if (!_workspace.Settings.ParanoiaMode)
            {
                // Single-Select außerhalb des Paranoia-Modus: alle anderen abwählen
                list.Clear();
                list.Add(id);
                _suppressToggle = true;
                try
                {
                    foreach (var (otherId, cb) in _checkboxes)
                        cb.IsChecked = (otherId == id);
                }
                finally { _suppressToggle = false; }
            }
            else if (!list.Contains(id))
            {
                list.Add(id);
            }
        }
        else
        {
            list.Remove(id);
        }
        _workspace.SaveSettings();
        UpdateValidation();
    }

    private void OnParanoiaToggled(object sender, ToggledEventArgs e)
    {
        _workspace.Settings.ParanoiaMode = e.Value;

        // beim Verlassen des Paranoia-Modus auf Single-Select reduzieren
        if (!e.Value && _workspace.Settings.SelectedTsaIds.Count > 1)
        {
            var keep = _workspace.Settings.SelectedTsaIds[0];
            _workspace.Settings.SelectedTsaIds.Clear();
            _workspace.Settings.SelectedTsaIds.Add(keep);
            _suppressToggle = true;
            try
            {
                foreach (var (otherId, cb) in _checkboxes)
                    cb.IsChecked = (otherId == keep);
            }
            finally { _suppressToggle = false; }
        }

        _workspace.SaveSettings();
        UpdateValidation();
    }

    private void UpdateValidation()
    {
        var msg = _workspace.Settings.Validate();
        ValidationLabel.Text = msg ?? "";
        if (_workspace.Settings.ParanoiaMode)
            TsaSectionHint.Text = $"Paranoia-Modus aktiv: genau 3 Services nötig (aktuell ausgewählt: {_workspace.Settings.SelectedTsaIds.Count}).";
        else
            TsaSectionHint.Text = "Wähle einen RFC3161-Dienst. Im Paranoia-Modus müssen genau drei ausgewählt sein.";
    }
}
