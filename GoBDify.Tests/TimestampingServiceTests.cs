using System.Security.Cryptography;
using System.Text;
using GoBDify.Core;
using Xunit;

namespace GoBDify.Tests;

public class TimestampingServiceTests
{
    private static readonly byte[] DemoHash =
        SHA256.HashData(Encoding.UTF8.GetBytes("GoBDify test payload"));

    /// <summary>
    /// Live-Test gegen alle bekannten TSAs. Trait("Category","Network") erlaubt
    /// Ausschluss in offline-CI per `dotnet test --filter Category!=Network`.
    /// </summary>
    [SkippableTheory]
    [Trait("Category", "Network")]
    [InlineData("certum")]
    [InlineData("freetsa")]
    [InlineData("digicert")]
    [InlineData("sectigo")]
    [InlineData("globalsign")]
    [InlineData("apple")]
    [InlineData("sslcom")]
    public async Task Jede_TSA_liefert_einen_gueltigen_Timestamp(string tsaId)
    {
        var tsa = TimestampAuthorities.ById(tsaId);
        Assert.NotNull(tsa);

        var svc = new TimestampingService();
        TimestampResult result;
        try
        {
            result = await svc.RequestAsync(tsa!, DemoHash, HashAlgorithmName.SHA256);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Skip.If(true, $"TSA '{tsa!.Name}' aktuell nicht erreichbar: {ex.Message}");
            return; // unreachable
        }

        Assert.NotNull(result.Token);
        Assert.NotEmpty(result.RawResponse);

        var verify = svc.Verify(DemoHash, HashAlgorithmName.SHA256, result.RawResponse);
        Assert.True(verify.IsValid, $"Verify schlug fehl: {verify.Error}");
        Assert.NotNull(verify.Timestamp);
        Assert.True((DateTimeOffset.UtcNow - verify.Timestamp!.Value).Duration() < TimeSpan.FromMinutes(10),
            $"Timestamp {verify.Timestamp:u} weicht zu stark von jetzt ab.");
    }

    [Fact]
    public async Task Phantasie_URL_wirft_HttpRequestException()
    {
        var svc = new TimestampingService();
        var fake = new TimestampAuthority("fake", "Fake", "http://gibts-bestimmt-nicht.invalid/tsr");

        await Assert.ThrowsAnyAsync<Exception>(() =>
            svc.RequestAsync(fake, DemoHash, HashAlgorithmName.SHA256));
    }

    [Fact]
    public async Task Nicht_routebare_Adresse_wirft_in_endlicher_Zeit()
    {
        // 127.0.0.1 Port 1 — verbindet nie (RST oder Timeout).
        var svc = new TimestampingService();
        var dead = new TimestampAuthority("dead", "Dead", "http://127.0.0.1:1/tsr");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await Assert.ThrowsAnyAsync<Exception>(() =>
            svc.RequestAsync(dead, DemoHash, HashAlgorithmName.SHA256));
        sw.Stop();

        // Der HttpClient-Timeout im Service ist 60s — wir verlangen <120s als Sanity-Bound.
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(120),
            $"Aufruf hing zu lange: {sw.Elapsed}");
    }

    [Fact]
    public async Task HTTP_Endpoint_der_kein_TSA_ist_wirft_Exception()
    {
        var svc = new TimestampingService();
        // Diese URL liefert HTML statt application/timestamp-reply.
        var html = new TimestampAuthority("html", "HTML", "http://example.com/");

        await Assert.ThrowsAnyAsync<Exception>(() =>
            svc.RequestAsync(html, DemoHash, HashAlgorithmName.SHA256));
    }

    [Fact]
    public void Verify_mit_korruptem_Token_meldet_Invalid_ohne_zu_werfen()
    {
        var svc = new TimestampingService();
        var garbage = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0xCA, 0xFE };

        var v = svc.Verify(DemoHash, HashAlgorithmName.SHA256, garbage);

        Assert.False(v.IsValid);
        Assert.NotNull(v.Error);
    }

    [Fact]
    public void TimestampAuthorities_ById_ist_case_insensitive_und_kennt_null_unbekannt()
    {
        Assert.NotNull(TimestampAuthorities.ById("certum"));
        Assert.NotNull(TimestampAuthorities.ById("CERTUM"));
        Assert.Null(TimestampAuthorities.ById("does-not-exist"));
    }

    [Fact]
    public void Defaults_listet_die_erwarteten_Anbieter()
    {
        var ids = TimestampAuthorities.Defaults.Select(t => t.Id).ToHashSet();
        foreach (var expected in new[] { "certum", "freetsa", "digicert", "sectigo", "globalsign", "apple", "sslcom" })
            Assert.Contains(expected, ids);
    }
}

