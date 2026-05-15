using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;

namespace GoBDify.Core;

public sealed record TimestampResult(
    TimestampAuthority Authority,
    Rfc3161TimestampToken Token,
    byte[] RawResponse);

public sealed record TimestampVerification(
    bool IsValid,
    DateTimeOffset? Timestamp,
    string? IssuerName,
    string? Error);

public class TimestampingService
{
    private const string RequestContentType = "application/timestamp-query";
    private const string ResponseContentType = "application/timestamp-reply";
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(60) };

    public async Task<TimestampResult> RequestAsync(
        TimestampAuthority authority,
        byte[] hash,
        HashAlgorithmName hashAlgorithmName,
        CancellationToken cancellationToken = default)
    {
        byte[] nonce = RandomNumberGenerator.GetBytes(20);
        var request = Rfc3161TimestampRequest.CreateFromHash(hash, hashAlgorithmName, null, nonce, true);
        byte[] body = request.Encode();

        using var content = new ByteArrayContent(body);
        content.Headers.ContentType = new MediaTypeHeaderValue(RequestContentType);

        using var msg = new HttpRequestMessage(HttpMethod.Post, authority.Url) { Content = content };
        using var resp = await HttpClient.SendAsync(msg, cancellationToken).ConfigureAwait(false);

        if (resp.StatusCode != HttpStatusCode.OK)
            throw new Exception($"Timestamp server '{authority.Name}' returned {resp.StatusCode}");

        byte[] respBytes = await resp.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        var token = request.ProcessResponse(respBytes, out _);
        if (!token.VerifySignatureForHash(hash, hashAlgorithmName, out _))
            throw new Exception($"Signature verification failed for response from '{authority.Name}'");

        return new TimestampResult(authority, token, respBytes);
    }

    public async Task<IReadOnlyList<TimestampResult>> RequestManyAsync(
        IEnumerable<TimestampAuthority> authorities,
        byte[] hash,
        HashAlgorithmName hashAlgorithmName,
        CancellationToken cancellationToken = default)
    {
        var tasks = authorities.Select(a => RequestAsync(a, hash, hashAlgorithmName, cancellationToken)).ToArray();
        return await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    public TimestampVerification Verify(byte[] hash, HashAlgorithmName hashAlgorithmName, byte[] rawResponse)
    {
        try
        {
            var dummy = Rfc3161TimestampRequest.CreateFromHash(hash, hashAlgorithmName, null, null, true);
            var token = dummy.ProcessResponse(rawResponse, out _);
            bool ok = token.VerifySignatureForHash(hash, hashAlgorithmName, out X509Certificate2? cert);
            return new TimestampVerification(ok, token.TokenInfo.Timestamp, cert?.IssuerName.Name, ok ? null : "signature invalid");
        }
        catch (Exception ex)
        {
            return new TimestampVerification(false, null, null, ex.Message);
        }
    }
}
