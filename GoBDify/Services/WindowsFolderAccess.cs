using System.Security.Cryptography;
using System.Text;

namespace GoBDify.Services;

/// <summary>
/// Hält Ordner-Zugriffe via Windows <c>FutureAccessList</c> über
/// App-Neustarts hinweg fest. Für Sideload-Apps mit <c>runFullTrust</c>
/// nicht zwingend nötig, für sandboxed Store-Builds essentiell — wir
/// schalten es generell ein, damit beide Distributionspfade identisch
/// funktionieren.
/// </summary>
public static class WindowsFolderAccess
{
    /// <summary>Stabiler 23-Zeichen-Token (alphanumerisch), eindeutig je Pfad.</summary>
    private static string TokenFor(string path)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(path.ToLowerInvariant()));
        // 8 Bytes → 16 Hex-Zeichen, plus 7-stelliger Prefix = 23 Zeichen, alphanumerisch.
        return "gobdify" + Convert.ToHexString(bytes, 0, 8).ToLowerInvariant();
    }

    public static Task RegisterAsync(string path)
    {
#if WINDOWS
        return RegisterCoreAsync(path);
#else
        _ = path;
        return Task.CompletedTask;
#endif
    }

    public static void Forget(string path)
    {
#if WINDOWS
        try
        {
            var token = TokenFor(path);
            var list = Windows.Storage.AccessCache.StorageApplicationPermissions.FutureAccessList;
            if (list.ContainsItem(token))
                list.Remove(token);
        }
        catch { /* best-effort */ }
#else
        _ = path;
#endif
    }

#if WINDOWS
    private static async Task RegisterCoreAsync(string path)
    {
        try
        {
            var folder = await Windows.Storage.StorageFolder.GetFolderFromPathAsync(path);
            Windows.Storage.AccessCache.StorageApplicationPermissions.FutureAccessList
                .AddOrReplace(TokenFor(path), folder);
        }
        catch
        {
            // Pfad existiert nicht mehr, keine Berechtigung, oder ist nicht für
            // FutureAccessList registrierbar (z. B. Netzlaufwerk ohne Mapping).
            // Wir schlucken den Fehler — der Audit-Lauf bringt dann eine
            // Fehlermeldung ans Tageslicht.
        }
    }
#endif
}
