using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.Diagnostics;

namespace GoBDify;

// Der folgende Code ist von https://github.com/t0xk/rfc3161-timestamping übernommen:

public static class Timestamping
{
    private static readonly HttpClient HttpClient = new HttpClient();
    private static readonly String RequestContentType = "application/timestamp-query";
    private static readonly String ResponseContentType = "application/timestamp-reply";
    private static readonly String TimestampAuthorityUrl = "http://time.certum.pl";  // "https://freetsa.org/tsr";  // 

    /// <summary>
    /// Requests a RFC3161 Timestamp from https://freetsa.org/tsr.
    /// Timestamping works in accordance with RFC3161(https://www.ietf.org/rfc/rfc3161.txt). HTTP is used as the
    /// transport protocol.
    /// </summary>
    /// <param name="hash">A string representation of a hash value</param>
    /// <param name="hashAlgorithmName">The hashing algorithm used when generating <paramref name="hash"/></param>
    /// <returns>An instance of Rfc3161TimestampToken</returns>
    /// <exception cref="Exception"></exception>
    public static async Task<(Rfc3161TimestampToken token, byte[] rawResponse)> RequestTimestampTokenForHash(byte[] hash, HashAlgorithmName hashAlgorithmName)
    {
        // A random nonce
        byte[] nonce = GetNonce(20);
        
        // Creating Rfc3161TimestampRequest instance, which will be used as the HTTP POST payload
        Rfc3161TimestampRequest timestampRequest =
            Rfc3161TimestampRequest.CreateFromHash(
                hash,
                hashAlgorithmName,
                null,
                nonce,
                true);

        // Construct HttpContent
        byte[] timestampRequestBytes = timestampRequest.Encode();
        using StreamContent streamContent = new StreamContent(new MemoryStream(timestampRequestBytes));
        streamContent
            .Headers
            .ContentType = new MediaTypeHeaderValue(RequestContentType);

        // Construct a HttpRequestMessage
        using HttpRequestMessage httpRequestMessage = new HttpRequestMessage();
        httpRequestMessage.RequestUri = new Uri(TimestampAuthorityUrl);
        httpRequestMessage.Method = HttpMethod.Post;
        httpRequestMessage.Content = streamContent;

        // Send the request and wait for a response
        using HttpResponseMessage httpResponseMessage = await HttpClient.SendAsync(httpRequestMessage);
        MediaTypeHeaderValue contentType = httpRequestMessage.Content.Headers.ContentType;

        if (httpResponseMessage.StatusCode != HttpStatusCode.OK)
            throw new Exception($"Timestamp Server returned a {httpResponseMessage.StatusCode} response");

        if (contentType is null)
            throw new Exception("Content-Type of Response can't be null");

        if (contentType.MediaType!.Equals(ResponseContentType))
            throw new Exception($"Invalid response Content-Type: {contentType.MediaType}");

        byte[] timestampResponseBytes = GetStreamBytes(httpResponseMessage.Content.ReadAsStream());

        int bytesConsumed;
        Rfc3161TimestampToken timestampToken =
            timestampRequest.ProcessResponse(timestampResponseBytes, out bytesConsumed);

        X509Certificate2? timestampAuthorityCertificate;
        if (!timestampToken.VerifySignatureForHash(hash, hashAlgorithmName, out timestampAuthorityCertificate))
            throw new Exception(
                $"Timestamp could not be verified for hash({hashAlgorithmName.Name}: {ToHex(hash, false)})");

        return (token: timestampToken, rawResponse: timestampResponseBytes);
    }
    
    /// <summary>
    /// Reads the contents of <paramref name="stream"/> as an array of bytes.
    /// </summary>
    /// <param name="stream">Stream we are reading.</param>
    /// <returns>An array of bytes representing the contents of the stream.</returns>
    private static byte[] GetStreamBytes(Stream stream)
    {
        List<byte> bytes = new List<byte>();

        using (stream)
        {
            while (true)
            {
                int b = stream.ReadByte();
                if (b == -1) break;

                bytes.Add((byte)b);
            }
        }

        return bytes.ToArray();
    }
    
    /// <summary>
    /// Generates a random nonce with a length of <paramref name="length"/>
    /// </summary>
    /// <param name="length">Length of the nonce (in bytes).</param>
    /// <returns>Byte array representing the nonce value</returns>
    private static byte[] GetNonce(int length)
    {
        byte[] outBytes = new byte[length];

        using (RandomNumberGenerator randomNumberGenerator = RandomNumberGenerator.Create())
        {
            randomNumberGenerator.GetBytes(outBytes);
        }

        return outBytes;
    }

    private static string ToHex(this byte[] bytes, bool upperCase)
    {
        StringBuilder result = new StringBuilder(bytes.Length * 2);

        for (int i = 0; i < bytes.Length; i++)
            result.Append(bytes[i].ToString(upperCase ? "X2" : "x2"));

        return result.ToString();
    }
}
