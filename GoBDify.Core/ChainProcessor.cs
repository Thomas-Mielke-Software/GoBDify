using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace GoBDify.Core;

public sealed record ProgressInfo(double Fraction, string Message);

public class ChainProcessor
{
    private readonly TimestampingService _ts;
    private static readonly Regex Sha256Re = new(@"^timestamp(\d{5})\.sha256$", RegexOptions.Compiled);
    private static readonly Regex TstSwissRe = new(@"^timestamp(\d{5})_([a-z])\.sha256\.tst$", RegexOptions.Compiled);
    private static readonly Regex TstLegacyRe = new(@"^timestamp(\d{5})\.sha256\.tst$", RegexOptions.Compiled);

    public ChainProcessor(TimestampingService? ts = null)
    {
        _ts = ts ?? new TimestampingService();
    }

    public static bool IsChainArtifact(string fileName) =>
        Sha256Re.IsMatch(fileName) || TstLegacyRe.IsMatch(fileName) || TstSwissRe.IsMatch(fileName);

    public async Task<AuditReport> AuditAsync(
        string folderPath,
        IProgress<ProgressInfo>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(folderPath))
            return new AuditReport(folderPath, Array.Empty<ChainEntry>(), Array.Empty<string>(), 0, "Ordner existiert nicht.");

        var allFiles = new DirectoryInfo(folderPath).GetFiles("*");
        var chain = new List<ChainEntry>();
        var referenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int last = 0;

        var sha256Files = allFiles.Where(f => Sha256Re.IsMatch(f.Name)).OrderBy(f => f.Name).ToList();
        double step = sha256Files.Count > 0 ? 1.0 / sha256Files.Count : 1.0;
        double prog = 0;

        foreach (var sha in sha256Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var m = Sha256Re.Match(sha.Name);
            int n = int.Parse(m.Groups[1].Value);
            if (n > last) last = n;

            // Find timestamp tokens (legacy and/or swiss)
            var tokens = new List<TimestampTokenInfo>();
            string metaHash = await HashFileHexAsync(sha.FullName, cancellationToken);
            byte[] metaHashBytes = Convert.FromHexString(metaHash);

            string baseName = sha.Name.Substring(0, sha.Name.Length - ".sha256".Length);
            // legacy
            var legacyTst = allFiles.FirstOrDefault(f => f.Name.Equals($"{sha.Name}.tst", StringComparison.OrdinalIgnoreCase));
            if (legacyTst != null)
                tokens.Add(VerifyTstFile(legacyTst, "", metaHashBytes));
            // swiss
            foreach (var f in allFiles)
            {
                var sm = TstSwissRe.Match(f.Name);
                if (sm.Success && int.Parse(sm.Groups[1].Value) == n)
                    tokens.Add(VerifyTstFile(f, sm.Groups[2].Value, metaHashBytes));
            }

            // Parse file list
            var files = new List<FileEntry>();
            using (var fs = sha.OpenRead())
            using (var sr = new StreamReader(fs, Encoding.UTF8))
            {
                string? line;
                while ((line = await sr.ReadLineAsync(cancellationToken)) != null)
                {
                    if (line.Length < 67 || line.StartsWith("#")) continue;
                    string expectedHash = line.Substring(0, 64);
                    string fname = line.Substring(66);
                    referenced.Add(fname);

                    var listed = allFiles.FirstOrDefault(f => f.Name == fname);
                    if (listed == null)
                    {
                        files.Add(new FileEntry(fname, expectedHash, null, FileVerificationStatus.Missing));
                        continue;
                    }
                    string actual = await HashFileHexAsync(listed.FullName, cancellationToken);
                    var status = string.Equals(actual, expectedHash, StringComparison.OrdinalIgnoreCase)
                        ? FileVerificationStatus.Ok
                        : FileVerificationStatus.Modified;
                    files.Add(new FileEntry(fname, expectedHash, actual, status));
                }
            }

            chain.Add(new ChainEntry(n, sha.Name, files, tokens));
            prog += step;
            progress?.Report(new ProgressInfo(prog, $"Geprüft: {sha.Name}"));
        }

        // New files = not chain artifacts and not referenced
        var newFiles = allFiles
            .Where(f => !IsChainArtifact(f.Name) && !referenced.Contains(f.Name))
            .Select(f => f.Name)
            .OrderBy(n => n)
            .ToList();

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
        CancellationToken cancellationToken = default)
    {
        var validation = settings.Validate();
        if (validation != null)
            return new ChainResult(new AuditReport(folderPath, Array.Empty<ChainEntry>(), Array.Empty<string>(), 0, validation), null, null);

        var audit = await AuditAsync(folderPath, progress, cancellationToken);
        if (audit.Error != null || audit.NewFiles.Count == 0)
            return new ChainResult(audit, null, null);

        if (audit.LastChainNumber >= 99999)
            return new ChainResult(audit with { Error = "Maximale Anzahl Timestamps erreicht." }, null, null);

        int newNumber = audit.LastChainNumber + 1;
        var authorities = settings.ResolveAuthorities().ToList();
        bool swiss = settings.SwissMode;

        // Build .sha256
        var sb = new StringBuilder();
        sb.Append($"# Dokument-Hashes überprüfen\n");
        sb.Append($"#   - unter Linux: sha256sum -c timestamp{newNumber:D5}.sha256\n");
        sb.Append($"#   - auf macOS: shasum -a 256 -c timestamp{newNumber:D5}.sha256\n");
        sb.Append($"#   - in Windows Powershell (nur einzelner Hash): Get-Filehash datei.pdf -Algorithm SHA256\n");
        sb.Append($"# Timestamp dieser .sha256-Datei verifizieren:\n");
        if (swiss)
        {
            for (int i = 0; i < authorities.Count; i++)
                sb.Append($"#   - openssl ts -verify -data timestamp{newNumber:D5}.sha256 -in timestamp{newNumber:D5}_{(char)('a' + i)}.sha256.tst -CApath \"$(openssl version -d | cut -d '\\\"' -f 2)/certs/\"   # {authorities[i].Name}\n");
        }
        else
        {
            sb.Append($"#   - openssl ts -verify -data timestamp{newNumber:D5}.sha256 -in timestamp{newNumber:D5}.sha256.tst -CApath \"$(openssl version -d | cut -d '\\\"' -f 2)/certs/\"\n");
        }

        foreach (var fname in audit.NewFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string hash = await HashFileHexAsync(Path.Combine(folderPath, fname), cancellationToken);
            sb.Append(hash);
            sb.Append(" *");
            sb.Append(fname);
            sb.Append('\n');
        }

        string sha256Name = $"timestamp{newNumber:D5}.sha256";
        string sha256Path = Path.Combine(folderPath, sha256Name);
        File.WriteAllText(sha256Path, sb.ToString());
        progress?.Report(new ProgressInfo(0.9, $"Erstellt: {sha256Name}"));

        // Hash and request timestamp(s)
        string metaHash = await HashFileHexAsync(sha256Path, cancellationToken);
        byte[] metaHashBytes = Convert.FromHexString(metaHash);

        var results = await _ts.RequestManyAsync(authorities, metaHashBytes, HashAlgorithmName.SHA256, cancellationToken);
        var tokens = new List<TimestampTokenInfo>();
        for (int i = 0; i < results.Count; i++)
        {
            string tstName = swiss
                ? $"timestamp{newNumber:D5}_{(char)('a' + i)}.sha256.tst"
                : $"timestamp{newNumber:D5}.sha256.tst";
            string tstPath = Path.Combine(folderPath, tstName);
            File.WriteAllBytes(tstPath, results[i].RawResponse);
            tokens.Add(new TimestampTokenInfo(
                swiss ? ((char)('a' + i)).ToString() : "",
                tstName,
                TimestampStatus.Valid,
                results[i].Token.TokenInfo.Timestamp,
                results[i].Authority.Name,
                null));
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
