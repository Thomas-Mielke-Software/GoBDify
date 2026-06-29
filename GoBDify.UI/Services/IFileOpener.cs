namespace GoBDify.Services;

/// <summary>
/// Plattform-Abstraktion zum Öffnen und Einlesen einer Textdatei über einen
/// nativen Datei-Auswahldialog. Gegenstück zu <see cref="IFileSaver"/>.
/// </summary>
public interface IFileOpener
{
    /// <summary>Öffnet den Datei-Dialog und liest den Inhalt der gewählten Datei (UTF-8).</summary>
    /// <returns>Dateiinhalt, oder <c>null</c> bei Abbruch.</returns>
    Task<string?> OpenTextAsync(CancellationToken ct = default);
}
