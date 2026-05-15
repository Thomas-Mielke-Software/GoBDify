using GoBDify.Core;
using GoBDify.Services;
using Microsoft.Maui.Controls.Shapes;

namespace GoBDify.Views;

public partial class SettingsPage : ContentPage
{
    private readonly WorkspaceService _workspace;
    private readonly Dictionary<string, CheckBox> _checkboxes = new();

    public SettingsPage()
    {
        InitializeComponent();
        _workspace = IPlatformApplication.Current!.Services.GetRequiredService<WorkspaceService>();
        BuildTsaList();
        SwissSwitch.IsToggled = _workspace.Settings.SwissMode;
        UpdateValidation();
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

    private void OnTsaToggled(string id, bool value)
    {
        var list = _workspace.Settings.SelectedTsaIds;
        if (value && !list.Contains(id)) list.Add(id);
        else if (!value) list.Remove(id);
        _workspace.SaveSettings();
        UpdateValidation();
    }

    private void OnSwissToggled(object sender, ToggledEventArgs e)
    {
        _workspace.Settings.SwissMode = e.Value;
        _workspace.SaveSettings();
        UpdateValidation();
    }

    private void UpdateValidation()
    {
        var msg = _workspace.Settings.Validate();
        ValidationLabel.Text = msg ?? "";
        if (_workspace.Settings.SwissMode)
            TsaSectionHint.Text = $"Schweiz-Modus aktiv: genau 3 Services nötig (aktuell ausgewählt: {_workspace.Settings.SelectedTsaIds.Count}).";
        else
            TsaSectionHint.Text = "Wähle einen oder mehrere RFC3161-Dienste. Im Schweiz-Modus müssen genau drei ausgewählt sein.";
    }
}
