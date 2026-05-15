using System.Text.Json;
using System.Text.Json.Serialization;

namespace GoBDify.Core;

public class AppSettings
{
    public List<string> SelectedTsaIds { get; set; } = new() { "certum" };
    public bool ParanoiaMode { get; set; } = false;
    public List<string> RecentFolders { get; set; } = new();
    public string? LastFolder { get; set; }

    public double? WindowX { get; set; }
    public double? WindowY { get; set; }
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }

    public IEnumerable<TimestampAuthority> ResolveAuthorities()
    {
        foreach (var id in SelectedTsaIds)
        {
            var tsa = TimestampAuthorities.ById(id);
            if (tsa != null) yield return tsa;
        }
    }

    public string? Validate()
    {
        var resolved = ResolveAuthorities().ToList();
        if (resolved.Count == 0) return "Bitte mindestens einen Timestamp-Service auswählen.";
        if (ParanoiaMode && resolved.Count != 3) return "Der Paranoia-Modus erfordert genau drei Timestamp-Services.";
        return null;
    }
}

// Source-generierter Kontext: AOT- und Trim-sicher.
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppSettings))]
internal partial class AppSettingsJsonContext : JsonSerializerContext
{
}

public static class AppSettingsStore
{
    public static string DefaultPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GoBDify",
            "settings.json");

    public static AppSettings Load(string? path = null)
    {
        path ??= DefaultPath;
        if (!File.Exists(path)) return new AppSettings();
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize(json, AppSettingsJsonContext.Default.AppSettings) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings, string? path = null)
    {
        path ??= DefaultPath;
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, JsonSerializer.Serialize(settings, AppSettingsJsonContext.Default.AppSettings));
    }
}
