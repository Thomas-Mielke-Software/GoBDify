using System.Security.Cryptography;
using System.Text;
using GoBDify.Core;
using Xunit;

namespace GoBDify.Tests;

public class ChainProcessorTests : IDisposable
{
    private readonly string _dir;

    public ChainProcessorTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "gobdify-tests-" + Path.GetRandomFileName());
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private string WriteFile(string name, string content)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, content);
        return path;
    }

    private static string Sha256Hex(string content)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>Erzeugt eine timestampNNNNN.sha256 ohne TST (für Audit-Logik-Tests).</summary>
    private void WriteShaFile(int n, IEnumerable<(string FileName, string Hash)> entries)
    {
        var sb = new StringBuilder();
        sb.Append("# test\n");
        foreach (var (name, hash) in entries)
            sb.Append($"{hash} *{name}\n");
        File.WriteAllText(Path.Combine(_dir, $"timestamp{n:D5}.sha256"), sb.ToString());
    }

    [Fact]
    public async Task Audit_unveraenderte_Dateien_meldet_Ok()
    {
        WriteFile("a.txt", "hello");
        WriteFile("b.txt", "world");
        WriteShaFile(1, new[]
        {
            ("a.txt", Sha256Hex("hello")),
            ("b.txt", Sha256Hex("world")),
        });

        var report = await new ChainProcessor().AuditAsync(_dir);

        Assert.Null(report.Error);
        Assert.Single(report.Chain);
        Assert.All(report.Chain[0].Files, f => Assert.Equal(FileVerificationStatus.Ok, f.Status));
        Assert.Empty(report.NewFiles);
    }

    [Fact]
    public async Task Audit_veraenderte_Datei_wird_als_Modified_gemeldet()
    {
        WriteFile("a.txt", "original");
        WriteShaFile(1, new[] { ("a.txt", Sha256Hex("original")) });

        // jetzt nachträglich verändern
        WriteFile("a.txt", "TAMPERED");

        var report = await new ChainProcessor().AuditAsync(_dir);

        Assert.Null(report.Error);
        Assert.Single(report.Chain);
        var entry = Assert.Single(report.Chain[0].Files);
        Assert.Equal(FileVerificationStatus.Modified, entry.Status);
        Assert.Equal("a.txt", entry.FileName);
        Assert.Equal(Sha256Hex("TAMPERED"), entry.ActualHash);
    }

    [Fact]
    public async Task Audit_fehlende_Datei_wird_als_Missing_gemeldet()
    {
        WriteFile("a.txt", "x");
        WriteFile("b.txt", "y");
        WriteShaFile(1, new[]
        {
            ("a.txt", Sha256Hex("x")),
            ("b.txt", Sha256Hex("y")),
            ("c.txt", Sha256Hex("never existed")),
        });

        var report = await new ChainProcessor().AuditAsync(_dir);

        Assert.Single(report.Chain);
        var files = report.Chain[0].Files;
        Assert.Equal(3, files.Count);
        Assert.Equal(FileVerificationStatus.Missing, files.Single(f => f.FileName == "c.txt").Status);
        Assert.Equal(FileVerificationStatus.Ok, files.Single(f => f.FileName == "a.txt").Status);
        Assert.Equal(FileVerificationStatus.Ok, files.Single(f => f.FileName == "b.txt").Status);
    }

    [Fact]
    public async Task Audit_findet_neue_Datei_ausserhalb_der_Kette()
    {
        WriteFile("known.txt", "k");
        WriteShaFile(1, new[] { ("known.txt", Sha256Hex("k")) });
        WriteFile("brandneu.pdf", "anything");

        var report = await new ChainProcessor().AuditAsync(_dir);

        Assert.Single(report.NewFiles);
        Assert.Equal("brandneu.pdf", report.NewFiles[0]);
    }

    [Fact]
    public async Task Audit_kombiniert_veraendert_fehlend_neu_in_einem_Lauf()
    {
        WriteFile("ok.txt", "ok");
        WriteFile("tampered.txt", "after");
        // gone.txt fehlt
        WriteFile("extra.txt", "neu");

        WriteShaFile(1, new[]
        {
            ("ok.txt", Sha256Hex("ok")),
            ("tampered.txt", Sha256Hex("before")),
            ("gone.txt", Sha256Hex("egal")),
        });

        var report = await new ChainProcessor().AuditAsync(_dir);

        var files = report.Chain[0].Files.ToDictionary(f => f.FileName);
        Assert.Equal(FileVerificationStatus.Ok,       files["ok.txt"].Status);
        Assert.Equal(FileVerificationStatus.Modified, files["tampered.txt"].Status);
        Assert.Equal(FileVerificationStatus.Missing,  files["gone.txt"].Status);
        Assert.Contains("extra.txt", report.NewFiles);
    }

    [Fact]
    public async Task Audit_emittiert_Events_in_korrekter_Reihenfolge()
    {
        WriteFile("a.txt", "a");
        WriteFile("b.txt", "b");
        WriteShaFile(1, new[]
        {
            ("a.txt", Sha256Hex("a")),
            ("b.txt", Sha256Hex("b")),
        });

        var events = new List<ChainEvent>();
        var collector = new Progress<ChainEvent>(events.Add);

        await new ChainProcessor().AuditAsync(_dir, events: collector);

        // Wir erwarten: ChainDiscovered für die eine Kette, dann je 2x Start+Completed
        // und schließlich AuditCompleted.
        Assert.Contains(events, e => e is ChainDiscovered);
        Assert.Equal(2, events.OfType<FileHashStarted>().Count());
        Assert.Equal(2, events.OfType<FileHashCompleted>().Count());
        Assert.Single(events.OfType<AuditCompleted>());

        // Reihenfolge: ChainDiscovered MUSS vor erstem FileHashStarted kommen
        var discoveredIdx = events.FindIndex(e => e is ChainDiscovered);
        var firstStartIdx = events.FindIndex(e => e is FileHashStarted);
        Assert.True(discoveredIdx < firstStartIdx);
    }

    [Fact]
    public async Task Audit_meldet_Fehler_bei_nicht_existentem_Ordner()
    {
        var report = await new ChainProcessor().AuditAsync(Path.Combine(_dir, "gibts-nicht"));
        Assert.NotNull(report.Error);
        Assert.Empty(report.Chain);
    }

    [Fact]
    public void IsChainArtifact_erkennt_alle_Varianten()
    {
        Assert.True(ChainProcessor.IsChainArtifact("timestamp00001.sha256"));
        Assert.True(ChainProcessor.IsChainArtifact("timestamp99999.sha256"));
        Assert.True(ChainProcessor.IsChainArtifact("timestamp00001.sha256.tst"));
        Assert.True(ChainProcessor.IsChainArtifact("timestamp00001_a.sha256.tst"));
        Assert.True(ChainProcessor.IsChainArtifact("timestamp00001_c.sha256.tst"));
        Assert.False(ChainProcessor.IsChainArtifact("timestamp0001.sha256"));
        Assert.False(ChainProcessor.IsChainArtifact("timestamp00001.sha"));
        Assert.False(ChainProcessor.IsChainArtifact("readme.md"));
        Assert.False(ChainProcessor.IsChainArtifact("timestamp00001.sha256.bak"));
    }
}
