using GoBDify.Services;
using GoBDify.Views;
using Gtk;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Platforms.Linux.Gtk4.Handlers;

namespace GoBDify.Platform;

/// <summary>
/// Eigener Shell-Handler für den Linux/GTK4-Head, der die Ordnerverwaltung
/// (<see cref="FlyoutFolderPanel"/>) in den linken Flyout-Bereich rendert.
///
/// Das GTK4-Backend baut den Flyout als feste Struktur — Text-Header-Label,
/// Item-<c>ListBox</c> (Übersicht/Einstellungen), Text-Footer-Label — und ignoriert
/// <c>FlyoutContentTemplate</c>. Wir greifen nach dem Aufbau in den Widget-Baum:
/// <c>PlatformView(Box) → Paned → StartChild(flyout-Box)</c> und setzen unsere
/// per <c>ToPlatform</c> erzeugte Ordner-View direkt unter dem Header ein. Die
/// vorhandene Item-ListBox bekommt <c>vexpand=false</c>, damit unsere Liste den
/// restlichen Platz erhält.
/// </summary>
public sealed class FolderFlyoutShellHandler : ShellHandler
{
    protected override void ConnectHandler(Box platformView)
    {
        base.ConnectHandler(platformView);
        TryInsertFolderPanel(platformView);
    }

    private void TryInsertFolderPanel(Box outer)
    {
        if (MauiContext is null) return;
        var workspace = MauiContext.Services.GetService<WorkspaceService>();
        if (workspace is null) return;

        // outer(Box) → Paned → StartChild = Flyout-Box
        if (outer.GetFirstChild() is not Paned paned) return;
        if (paned.GetStartChild() is not Box flyoutBox) return;

        var panel = new FlyoutFolderPanel(workspace);
        var widget = (Widget)panel.ToPlatform(MauiContext);
        widget.SetVexpand(true);
        widget.SetHexpand(true);

        // Die Ordner-Zeilen-Buttons linksbündig ausrichten — GTK zentriert Button-
        // Labels per Default und MAUI bietet dafür keine Eigenschaft. Nach jedem
        // Listen-Neuaufbau (und initial) über einen Idle-Callback nacharbeiten, damit
        // die frisch erzeugten Button-Widgets schon existieren.
        void ScheduleAlign() => GLib.Functions.IdleAdd(0, () => { LeftAlignFolderRows(widget); return false; });
        panel.ListRendered += ScheduleAlign;
        ScheduleAlign();

        // Die Item-ListBox auf Inhaltsgröße schrumpfen, damit unsere Liste wächst.
        for (var child = flyoutBox.GetFirstChild(); child != null; child = child.GetNextSibling())
            if (child is ListBox listBox)
                listBox.SetVexpand(false);

        // Direkt unter dem Header-Label einsetzen.
        var header = flyoutBox.GetFirstChild();
        if (header != null)
            flyoutBox.InsertChildAfter(widget, header);
        else
            flyoutBox.Prepend(widget);
    }

    /// <summary>
    /// Richtet im GTK-Baum die Label aller mit <see cref="FlyoutFolderPanel.FolderRowName"/>
    /// benannten Buttons (→ <c>widget.SetName</c> via AutomationId) linksbündig aus.
    /// Icon- und Aktionsknöpfe (ohne diesen Namen) bleiben zentriert.
    /// </summary>
    private static void LeftAlignFolderRows(Widget root)
    {
        if (root is Gtk.Button button && button.GetName() == FlyoutFolderPanel.FolderRowName
            && button.GetChild() is Gtk.Label label)
        {
            label.SetXalign(0f);
            ((Widget)label).SetHalign(Align.Fill);
            ((Widget)label).SetHexpand(true);
        }

        for (var child = root.GetFirstChild(); child != null; child = child.GetNextSibling())
            LeftAlignFolderRows(child);
    }
}
