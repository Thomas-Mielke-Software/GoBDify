namespace GoBDify
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // Das GTK4-Backend rendert den FlyoutHeader nur als Text (ToString) —
            // ein Grid würde dort als "Microsoft.Maui.Controls.Grid" erscheinen.
            // Auf Linux deshalb einen schlichten Text-Header setzen; Windows behält
            // das gestaltete Grid aus AppShell.xaml.
            if (OperatingSystem.IsLinux())
            {
                FlyoutHeader = "GoBDify";
                // Die Ordnerverwaltung liegt auf Linux im Flyout — das Item heißt
                // dort passender "Ordneransicht".
                HomeShellContent.Title = "Ordneransicht";
            }
        }
    }
}
