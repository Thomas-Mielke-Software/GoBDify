namespace GoBDify.Services;

/// <summary>
/// Hilfen rund um die MAUI-Drag&amp;Drop-Gesten.
/// </summary>
public static class DragDropHelper
{
    /// <summary>
    /// Passt das Windows-Drag-Visual an: blendet das Operations-Glyph (das "+"
    /// bzw. das Verbots-Symbol) aus und setzt die Beschriftung auf "Verschieben".
    /// Hintergrund: MAUIs plattformneutrales <c>DataPackageOperation</c> kennt nur
    /// <c>None</c>/<c>Copy</c>. <c>Copy</c> ist nötig, damit der Drop überhaupt
    /// auslöst, zeigt aber "Kopieren". Den irreführenden Text/Glyph korrigieren wir
    /// über die WinUI-<c>DragUIOverride</c> — reflektiv, da diese geteilte net10.0-
    /// Library keine Plattform-Typen referenzieren kann. Auf anderen Plattformen
    /// (keine passenden Args) ist das ein No-op.
    /// </summary>
    public static void SetMoveVisual(DragEventArgs e)
    {
        var platformArgs = e.GetType().GetProperty("PlatformArgs")?.GetValue(e);
        var nativeArgs = platformArgs?.GetType().GetProperty("DragEventArgs")?.GetValue(platformArgs);
        var uiOverride = nativeArgs?.GetType().GetProperty("DragUIOverride")?.GetValue(nativeArgs);
        if (uiOverride is null) return;

        var t = uiOverride.GetType();
        t.GetProperty("Caption")?.SetValue(uiOverride, "Verschieben");
        t.GetProperty("IsGlyphVisible")?.SetValue(uiOverride, false);
        t.GetProperty("IsCaptionVisible")?.SetValue(uiOverride, true);
    }
}
