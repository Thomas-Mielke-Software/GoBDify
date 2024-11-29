using System;
using System.Security.Cryptography.Pkcs;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GoBDify;

// Der folgende Code ist von https://github.com/t0xk/rfc3161-timestamping übernommen:

/// <summary>
/// A <c>JsonConverter</c> for serializing <c>Rfc3161TimestampToken</c> to and from JSON.
/// </summary>
public class Rfc3161TimestampTokenJsonConverter : JsonConverter<Rfc3161TimestampToken>
{
    public override Rfc3161TimestampToken? Read(ref Utf8JsonReader reader, Type typeToConvert,
        JsonSerializerOptions options)
    {
        Rfc3161TimestampToken? timestampToken;
        int bytesConsumed;
        Rfc3161TimestampToken.TryDecode(
          reader.GetBytesFromBase64(),
          out timestampToken,
          out bytesConsumed
        );
        
        if(timestampToken is null)
          throw new Exception("Timestamp token converted as null.");

        return timestampToken;
    }

    public override void Write(Utf8JsonWriter writer, Rfc3161TimestampToken value, JsonSerializerOptions options)
    {
        writer.WriteBase64StringValue(
            value
                .AsSignedCms()
                .Encode()
        );
    }
}
