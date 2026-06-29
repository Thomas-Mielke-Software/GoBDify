namespace GoBDify.Services;

/// <summary>
/// Plattform-Abstraktion zum Speichern einer Datei über einen nativen
/// "Speichern unter"-Dialog. Analog zu <see cref="IFolderPicker"/> kapselt die
/// jeweilige Head-Implementierung den plattformspezifischen Dialog; die geteilte
/// UI baut nur den Textinhalt und ruft hier hinein.
/// </summary>
public interface IFileSaver
{
    /// <summary>
    /// Öffnet den Speichern-Dialog mit <paramref name="suggestedFileName"/> als
    /// Vorschlag und schreibt <paramref name="content"/> (UTF-8) in die gewählte Datei.
    /// </summary>
    /// <returns>Pfad der gespeicherten Datei, oder <c>null</c> bei Abbruch.</returns>
    Task<string?> SaveTextAsync(string suggestedFileName, string content, CancellationToken ct = default);
}
