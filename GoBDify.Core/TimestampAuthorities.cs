namespace GoBDify.Core;

public sealed record TimestampAuthority(string Id, string Name, string Url);

public static class TimestampAuthorities
{
    public static readonly IReadOnlyList<TimestampAuthority> Defaults = new[]
    {
        new TimestampAuthority("certum",    "Certum",    "http://time.certum.pl"),
        new TimestampAuthority("freetsa",   "FreeTSA",   "https://freetsa.org/tsr"),
        new TimestampAuthority("digicert",  "DigiCert",  "http://timestamp.digicert.com"),
        new TimestampAuthority("sectigo",   "Sectigo",   "http://timestamp.sectigo.com"),
        new TimestampAuthority("globalsign","GlobalSign","http://timestamp.globalsign.com/tsa/r6advanced1"),
        new TimestampAuthority("apple",     "Apple",     "http://timestamp.apple.com/ts01"),
        new TimestampAuthority("sslcom",    "SSL.com",   "http://ts.ssl.com"),
    };

    public static TimestampAuthority? ById(string id) =>
        Defaults.FirstOrDefault(t => t.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
}
