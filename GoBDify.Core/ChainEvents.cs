namespace GoBDify.Core;

public abstract record ChainEvent;

/// <summary>Skeleton einer bestehenden Hash-Kette gefunden, vor dem Hashen.</summary>
public sealed record ChainDiscovered(
    int ChainIndex,
    int ChainNumber,
    string Sha256FileName,
    IReadOnlyList<string> FileNames,
    IReadOnlyList<(string TsaSuffix, string TstFileName)> Timestamps) : ChainEvent;

public sealed record FileHashStarted(int ChainIndex, string FileName) : ChainEvent;

public sealed record FileHashCompleted(
    int ChainIndex,
    string FileName,
    FileVerificationStatus Status,
    string? ActualHash) : ChainEvent;

public sealed record TimestampVerified(
    int ChainIndex,
    string TstFileName,
    string TsaSuffix,
    TimestampStatus Status,
    DateTimeOffset? Timestamp,
    string? IssuerName,
    string? Error) : ChainEvent;

/// <summary>Audit fertig. Zusätzlich werden neue, nicht zugeordnete Dateien gemeldet.</summary>
public sealed record AuditCompleted(IReadOnlyList<string> NewFiles) : ChainEvent;

/// <summary>Eine neue Kette wird erzeugt (vor dem Hashen).</summary>
public sealed record NewChainStarting(
    int ChainIndex,
    int ChainNumber,
    string Sha256FileName,
    IReadOnlyList<string> FileNames) : ChainEvent;

public sealed record NewFileHashStarted(int ChainIndex, string FileName) : ChainEvent;

public sealed record NewFileHashCompleted(int ChainIndex, string FileName, string Hash) : ChainEvent;

public sealed record NewTimestampRequested(
    int ChainIndex,
    string TsaName,
    string TstFileName,
    DateTimeOffset? Timestamp) : ChainEvent;
