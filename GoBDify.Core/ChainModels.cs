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
    IReadOnlyList<TimestampTokenInfo> Timestamps);

public sealed record AuditReport(
    string FolderPath,
    IReadOnlyList<ChainEntry> Chain,
    IReadOnlyList<string> NewFiles,
    int LastChainNumber,
    string? Error)
{
    /// <summary>
    /// True, wenn das Audit ein GoBD-relevantes Problem gefunden hat: veränderte oder
    /// fehlende Dateien, einen ungültigen/unlesbaren Zeitstempel, oder eine .sha256 ganz
    /// ohne Zeitstempel-Token (z. B. weil die Timestamp-Anfrage seinerzeit scheiterte).
    /// </summary>
    public bool HasIssues =>
        Chain.Any(c =>
            c.Files.Any(f => f.Status is FileVerificationStatus.Modified or FileVerificationStatus.Missing)
            || c.Timestamps.Any(t => t.Status != TimestampStatus.Valid)
            || c.Timestamps.Count == 0);
}

public sealed record ChainResult(
    AuditReport Audit,
    int? NewChainNumber,
    IReadOnlyList<TimestampTokenInfo>? NewTimestamps);
