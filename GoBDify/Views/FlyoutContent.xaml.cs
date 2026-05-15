using CommunityToolkit.Maui.Storage;
using GoBDify.Services;

namespace GoBDify.Views;

public partial class FlyoutContent : ContentView
{
    private readonly WorkspaceService _workspace;

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
        foreach (var folder in _workspace.RecentFolders)
        {
            var path = folder;
            bool active = string.Equals(path, _workspace.CurrentFolder, StringComparison.OrdinalIgnoreCase);
            var label = new Label
            {
                Text = ShortName(path),
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

            var border = new Border
            {
                Padding = new Thickness(10, 6, 6, 6),
                StrokeThickness = 0,
                BackgroundColor = active ? Color.FromArgb("#374151") : Colors.Transparent,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 6 },
                Content = grid
            };
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, __) => { _workspace.CurrentFolder = path; Shell.Current.FlyoutIsPresented = false; Shell.Current.GoToAsync("//home"); };
            stack.GestureRecognizers.Add(tap);
            RecentList.Children.Add(border);
        }
    }

    private static string ShortName(string path)
    {
        try { return new DirectoryInfo(path).Name; } catch { return path; }
    }

    private async void OnAddFolderClicked(object sender, EventArgs e)
    {
        try
        {
            var result = await FolderPicker.PickAsync(_workspace.CurrentFolder ?? string.Empty, default);
            if (result?.Folder?.Path == null) return;
            _workspace.CurrentFolder = result.Folder.Path;
            Shell.Current.FlyoutIsPresented = false;
            await Shell.Current.GoToAsync("//home");
        }
        catch (Exception ex)
        {
            await Application.Current!.MainPage!.DisplayAlert("Fehler", ex.Message, "OK");
        }
    }

    private async void OnSettingsClicked(object sender, EventArgs e)
    {
        Shell.Current.FlyoutIsPresented = false;
        await Shell.Current.GoToAsync("//settings");
    }
}
