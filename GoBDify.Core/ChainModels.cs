namespace GoBDify.Core;

public enum FileVerificationStatus
{
    Ok,
    Modified,
    Missing,
    New,
}

public enum TimestampStatus
{
    Valid,
    Invalid,
    Unreadable,
}

public sealed record FileEntry(
    string FileName,
    string ExpectedHash,
    string? ActualHash,
    FileVerificationStatus Status);

public sealed record TimestampTokenInfo(
    string TsaFileSuffix,
    string TstFileName,
    TimestampStatus Status,
    DateTimeOffset? Timestamp,
    string? IssuerName,
    string? Error);

public sealed record ChainEntry(
    int Number,
    string Sha256FileName,
    IReadOnlyList<FileEntry> Files,
    IReadOnlyList<TimestampTokenInfo> Timestamps)
{
    /// <summary>
    /// Fehlende Suffix-Buchstaben innerhalb eines Paranoia-Token-Satzes — z. B. 'b',
    /// wenn nur <c>_a</c> und <c>_c</c> vorliegen. Eine echte Lücke in der Buchstabenfolge
    /// kann nicht legitim sein und bedeutet ein verlorenes .tst. (Ein komplett fehlendes
    /// Token, also gar kein .tst, wird separat über <see cref="Timestamps"/>.Count geprüft;
    /// ob ursprünglich 1 oder 3 TSAs gewünscht waren, lässt sich ohne Settings nicht sagen.)
    /// </summary>
    public IReadOnlyList<char> MissingTimestampSuffixes
    {
        get
        {
            var letters = Timestamps
                .Select(t => t.TsaFileSuffix)
                .Where(s => s.Length == 1 && s[0] is >= 'a' and <= 'z')
                .Select(s => s[0])
                .ToHashSet();
            if (letters.Count == 0) return Array.Empty<char>();
            char max = letters.Max();
            var missing = new List<char>();
            for (char c = 'a'; c < max; c++)
                if (!letters.Contains(c)) missing.Add(c);
            return missing;
        }
    }
}

public sealed record AuditReport(
    string FolderPath,
    IReadOnlyList<ChainEntry> Chain,
    IReadOnlyList<string> NewFiles,
    int LastChainNumber,
    string? Error)
{
    /// <summary>
    /// Fehlende Kettennummern zwischen 1 und <see cref="LastChainNumber"/>, also Lücken in
    /// der Folge <c>timestamp00001.sha256</c>, <c>timestamp00002.sha256</c>, … Eine Lücke
    /// bedeutet, dass ein Glied der Kette fehlt — die lückenlose Folge ist Teil der
    /// GoBD-Nachvollziehbarkeit.
    /// </summary>
    public IReadOnlyList<int> MissingChainNumbers { get; init; } = Array.Empty<int>();

    /// <summary>
    /// True, wenn das Audit ein GoBD-relevantes Problem gefunden hat: veränderte oder
    /// fehlende Dateien, einen ungültigen/unlesbaren Zeitstempel, eine .sha256 ganz ohne
    /// Zeitstempel-Token (z. B. weil die Timestamp-Anfrage seinerzeit scheiterte), ein
    /// unvollständiger Paranoia-Token-Satz, oder eine Lücke in der Kettennummerierung.
    /// </summary>
    public bool HasIssues =>
        MissingChainNumbers.Count > 0
        || Chain.Any(c =>
            c.Files.Any(f => f.Status is FileVerificationStatus.Modified or FileVerificationStatus.Missing)
            || c.Timestamps.Any(t => t.Status != TimestampStatus.Valid)
            || c.Timestamps.Count == 0
            || c.MissingTimestampSuffixes.Count > 0);
}

public sealed record ChainResult(
    AuditReport Audit,
    int? NewChainNumber,
    IReadOnlyList<TimestampTokenInfo>? NewTimestamps);
