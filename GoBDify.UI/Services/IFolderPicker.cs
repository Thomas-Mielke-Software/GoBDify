namespace GoBDify.Services;

/// <summary>
/// Plattform-Abstraktion für die Ordnerauswahl. Die jeweilige Head-Implementierung
/// kümmert sich zusätzlich um etwaige plattformspezifische Zugriffspersistenz
/// (z. B. Windows <c>FutureAccessList</c>); die geteilte UI kennt davon nichts.
/// </summary>
public interface IFolderPicker
{
    /// <summary>Öffnet den Ordner-Dialog (mit optionaler Pfad-Vorauswahl).</summary>
    /// <returns>Gewählter Ordnerpfad, oder <c>null</c> bei Abbruch.</returns>
    Task<string?> PickFolderAsync(string? initialPath);

    /// <summary>
    /// Gibt eine zuvor gemerkte Zugriffsberechtigung für den Pfad wieder frei.
    /// No-op auf Plattformen ohne persistente Zugriffsverwaltung.
    /// </summary>
    void ForgetFolder(string path);
}
