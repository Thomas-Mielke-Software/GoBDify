using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace GoBDify.Core;

public sealed record ProgressInfo(double Fraction, string Message);

public class ChainProcessor
{
    private readonly TimestampingService _ts;
    private static readonly Regex Sha256Re = new(@"^timestamp(\d{5})\.sha256$", RegexOptions.Compiled);
    private static readonly Regex TstMultiRe = new(@"^timestamp(\d{5})_([a-z])\.sha256\.tst$", RegexOptions.Compiled);
    private static readonly Regex TstLegacyRe = new(@"^timestamp(\d{5})\.sha256\.tst$", RegexOptions.Compiled);

    public ChainProcessor(TimestampingService? ts = null)
    {
        _ts = ts ?? new TimestampingService();
    }

    public static bool IsChainArtifact(string fileName) =>
        Sha256Re.IsMatch(fileName) || TstLegacyRe.IsMatch(fileName) || TstMultiRe.IsMatch(fileName);

    private sealed record ChainSkeleton(
        int ChainIndex,
        int ChainNumber,
        FileInfo Sha256File,
        IReadOnlyList<(string FileName, string ExpectedHash)> Entries,
        IReadOnlyList<(FileInfo TstFile, string Suffix)> Timestamps);

    public async Task<AuditReport> AuditAsync(
        string folderPath,
        IProgress<ProgressInfo>? progress = null,
        IProgress<ChainEvent>? events = null,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(folderPath))
            return new AuditReport(folderPath, Array.Empty<ChainEntry>(), Array.Empty<string>(), 0, "Ordner existiert nicht.");

        var allFiles = new DirectoryInfo(folderPath).GetFiles("*");
        var sha256Files = allFiles.Where(f => Sha256Re.IsMatch(f.Name)).OrderBy(f => f.Name).ToList();

        // 1) Pre-discover all skeletons (parse .sha256 file lists, locate .tst siblings) without hashing
        var skeletons = new List<ChainSkeleton>();
        int last = 0;
        var referenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int idx = 0; idx < sha256Files.Count; idx++)
        {
            var sha = sha256Files[idx];
            int n = int.Parse(Sha256Re.Match(sha.Name).Groups[1].Value);
            if (n > last) last = n;

            var entries = new List<(string, string)>();
            using (var fs = sha.OpenRead())
            using (var sr = new StreamReader(fs, Encoding.UTF8))
            {
                string? line;
                while ((line = await sr.ReadLineAsync(cancellationToken)) != null)
                {
                    if (line.Length < 67 || line.StartsWith("#")) continue;
                    entries.Add((line.Substring(66), line.Substring(0, 64)));
                    referenced.Add(line.Substring(66));
                }
            }

            var tsts = new List<(FileInfo, string)>();
            var legacy = allFiles.FirstOrDefault(f => f.Name.Equals($"{sha.Name}.tst", StringComparison.OrdinalIgnoreCase));
            if (legacy != null) tsts.Add((legacy, ""));
            foreach (var f in allFiles)
            {
                var sm = TstMultiRe.Match(f.Name);
                if (sm.Success && int.Parse(sm.Groups[1].Value) == n)
                    tsts.Add((f, sm.Groups[2].Value));
            }

            skeletons.Add(new ChainSkeleton(idx, n, sha, entries, tsts));
            events?.Report(new ChainDiscovered(
                idx, n, sha.Name,
                entries.Select(e => e.Item1).ToList(),
                tsts.Select(t => (t.Item2, t.Item1.Name)).ToList()));
        }

        // 2) Hash + verify per skeleton, emitting per-file events
        var chain = new List<ChainEntry>();
        double step = skeletons.Count > 0 ? 1.0 / skeletons.Count : 1.0;
        double prog = 0;

        foreach (var sk in skeletons)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // verify timestamps (after hashing the .sha256 itself)
            string metaHash = await HashFileHexAsync(sk.Sha256File.FullName, cancellationToken);
            byte[] metaHashBytes = Convert.FromHexString(metaHash);

            var tokens = new List<TimestampTokenInfo>();
            foreach (var (tstFile, suffix) in sk.Timestamps)
            {
                var info = VerifyTstFile(tstFile, suffix, metaHashBytes);
                tokens.Add(info);
                events?.Report(new TimestampVerified(
                    sk.ChainIndex, info.TstFileName, info.TsaFileSuffix,
                    info.Status, info.Timestamp, info.IssuerName, info.Error));
            }

            // hash each referenced file
            var files = new List<FileEntry>();
            foreach (var (fname, expectedHash) in sk.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                events?.Report(new FileHashStarted(sk.ChainIndex, fname));

                var listed = allFiles.FirstOrDefault(f => f.Name == fname);
                if (listed == null)
                {
                    files.Add(new FileEntry(fname, expectedHash, null, FileVerificationStatus.Missing));
                    events?.Report(new FileHashCompleted(sk.ChainIndex, fname, FileVerificationStatus.Missing, null));
                    continue;
                }
                string actual = await HashFileHexAsync(listed.FullName, cancellationToken);
                var status = string.Equals(actual, expectedHash, StringComparison.OrdinalIgnoreCase)
                    ? FileVerificationStatus.Ok
                    : FileVerificationStatus.Modified;
                files.Add(new FileEntry(fname, expectedHash, actual, status));
                events?.Report(new FileHashCompleted(sk.ChainIndex, fname, status, actual));
            }

            chain.Add(new ChainEntry(sk.ChainNumber, sk.Sha256File.Name, files, tokens));
            prog += step;
            progress?.Report(new ProgressInfo(prog, $"Geprüft: {sk.Sha256File.Name}"));
        }

        var newFiles = allFiles
            .Where(f => !IsChainArtifact(f.Name) && !referenced.Contains(f.Name))
            .Select(f => f.Name)
            .OrderBy(n => n)
            .ToList();

        events?.Report(new AuditCompleted(newFiles));
        return new AuditReport(folderPath, chain, newFiles, last, null);
    }

    private TimestampTokenInfo VerifyTstFile(FileInfo tst, string suffix, byte[] metaHashBytes)
    {
        try
        {
            var raw = File.ReadAllBytes(tst.FullName);
            var v = _ts.Verify(metaHashBytes, HashAlgorithmName.SHA256, raw);
            return new TimestampTokenInfo(
                suffix,
                tst.Name,
                v.IsValid ? TimestampStatus.Valid : TimestampStatus.Invalid,
                v.Timestamp,
                v.IssuerName,
                v.Error);
        }
        catch (Exception ex)
        {
            return new TimestampTokenInfo(suffix, tst.Name, TimestampStatus.Unreadable, null, null, ex.Message);
        }
    }

    public async Task<ChainResult> AuditAndTimestampAsync(
        string folderPath,
        AppSettings settings,
        IProgress<ProgressInfo>? progress = null,
        IProgress<ChainEvent>? events = null,
        CancellationToken cancellationToken = default)
    {
        var validation = settings.Validate();
        if (validation != null)
            return new ChainResult(new AuditReport(folderPath, Array.Empty<ChainEntry>(), Array.Empty<string>(), 0, validation), null, null);

        var audit = await AuditAsync(folderPath, progress, events, cancellationToken);
        if (audit.Error != null || audit.NewFiles.Count == 0)
            return new ChainResult(audit, null, null);

        if (audit.LastChainNumber >= 99999)
            return new ChainResult(audit with { Error = "Maximale Anzahl Timestamps erreicht." }, null, null);

        int newNumber = audit.LastChainNumber + 1;
        int newIdx = audit.Chain.Count;
        string sha256Name = $"timestamp{newNumber:D5}.sha256";
        var authorities = settings.ResolveAuthorities().ToList();
        bool multi = settings.ParanoiaMode;

        events?.Report(new NewChainStarting(newIdx, newNumber, sha256Name, audit.NewFiles));

        // Build .sha256
        var sb = new StringBuilder();
        sb.Append($"# Dokument-Hashes überprüfen\n");
        sb.Append($"#   - unter Linux: sha256sum -c {sha256Name}\n");
        sb.Append($"#   - auf macOS: shasum -a 256 -c {sha256Name}\n");
        sb.Append($"#   - in Windows Powershell (nur einzelner Hash): Get-Filehash datei.pdf -Algorithm SHA256\n");
        sb.Append($"# Timestamp dieser .sha256-Datei verifizieren:\n");
        if (multi)
        {
            for (int i = 0; i < authorities.Count; i++)
                sb.Append($"#   - openssl ts -verify -data {sha256Name} -in timestamp{newNumber:D5}_{(char)('a' + i)}.sha256.tst -CApath \"$(openssl version -d | cut -d '\\\"' -f 2)/certs/\"   # {authorities[i].Name}\n");
        }
        else
        {
            sb.Append($"#   - openssl ts -verify -data {sha256Name} -in {sha256Name}.tst -CApath \"$(openssl version -d | cut -d '\\\"' -f 2)/certs/\"\n");
        }

        foreach (var fname in audit.NewFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            events?.Report(new NewFileHashStarted(newIdx, fname));
            string hash = await HashFileHexAsync(Path.Combine(folderPath, fname), cancellationToken);
            sb.Append(hash);
            sb.Append(" *");
            sb.Append(fname);
            sb.Append('\n');
            events?.Report(new NewFileHashCompleted(newIdx, fname, hash));
        }

        string sha256Path = Path.Combine(folderPath, sha256Name);
        File.WriteAllText(sha256Path, sb.ToString());
        progress?.Report(new ProgressInfo(0.9, $"Erstellt: {sha256Name}"));

        // Hash + RFC3161 request(s)
        string metaHash = await HashFileHexAsync(sha256Path, cancellationToken);
        byte[] metaHashBytes = Convert.FromHexString(metaHash);

        var results = await _ts.RequestManyAsync(authorities, metaHashBytes, HashAlgorithmName.SHA256, cancellationToken);
        var tokens = new List<TimestampTokenInfo>();
        for (int i = 0; i < results.Count; i++)
        {
            string tstName = multi
                ? $"timestamp{newNumber:D5}_{(char)('a' + i)}.sha256.tst"
                : $"timestamp{newNumber:D5}.sha256.tst";
            string tstPath = Path.Combine(folderPath, tstName);
            File.WriteAllBytes(tstPath, results[i].RawResponse);
            var info = new TimestampTokenInfo(
                multi ? ((char)('a' + i)).ToString() : "",
                tstName,
                TimestampStatus.Valid,
                results[i].Token.TokenInfo.Timestamp,
                results[i].Authority.Name,
                null);
            tokens.Add(info);
            events?.Report(new NewTimestampRequested(newIdx, results[i].Authority.Name, tstName, info.Timestamp));
        }
        progress?.Report(new ProgressInfo(1.0, "Fertig."));
        return new ChainResult(audit, newNumber, tokens);
    }

    public static async Task<string> HashFileHexAsync(string path, CancellationToken cancellationToken = default)
    {
        await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        using var sha = SHA256.Create();
        var buf = new byte[8192];
        int read;
        while ((read = await fs.ReadAsync(buf.AsMemory(0, buf.Length), cancellationToken)) != 0)
            sha.TransformBlock(buf, 0, read, null, 0);
        sha.TransformFinalBlock(buf, 0, 0);
        return Convert.ToHexString(sha.Hash!).ToLowerInvariant();
    }
}
