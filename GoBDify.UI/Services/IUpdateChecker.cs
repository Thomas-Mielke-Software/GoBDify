namespace GoBDify.Services;

/// <summary>Ergebnis-Kategorie einer Update-Prüfung.</summary>
public enum UpdateCheckStatus
{
    /// <summary>Diese Plattform/Installationsart kann nicht nach Updates suchen (z. B. Linux/GTK).</summary>
    NotSupported,
    /// <summary>App ist aktuell, keine neuere Version vorhanden.</summary>
    UpToDate,
    /// <summary>Update verfügbar, wird beim nächsten Start angewendet.</summary>
    Available,
    /// <summary>Pflicht-Update verfügbar.</summary>
    Required,
    /// <summary>Bei der Prüfung ist ein Fehler aufgetreten.</summary>
    Error,
    /// <summary>Status unklar — vermutlich nicht via AppInstaller installiert.</summary>
    Unknown,
}

/// <summary>Ergebnis einer Update-Prüfung samt optionaler Fehlermeldung.</summary>
public sealed record UpdateCheckResult(UpdateCheckStatus Status, string? Message = null);

/// <summary>
/// Plattform-Abstraktion für die „Nach Updates suchen"-Funktion. Die Windows-Implementierung
/// fragt den AppInstaller-Mechanismus ab; andere Heads liefern <see cref="UpdateCheckStatus.NotSupported"/>.
/// Liegt im gemeinsamen net10.0-UI-Projekt, wo das WINDOWS-Symbol nicht definiert ist — deshalb
/// darf die eigentliche WinRT-Abfrage nicht hier, sondern nur im Windows-Head stehen.
/// </summary>
public interface IUpdateChecker
{
    /// <summary>Prüft, ob für die laufende App eine neuere Version bereitsteht.</summary>
    Task<UpdateCheckResult> CheckAsync(CancellationToken ct = default);
}
