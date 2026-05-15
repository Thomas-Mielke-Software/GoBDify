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
    string? Error);

public sealed record ChainResult(
    AuditReport Audit,
    int? NewChainNumber,
    IReadOnlyList<TimestampTokenInfo>? NewTimestamps);
