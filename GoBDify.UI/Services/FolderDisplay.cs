namespace GoBDify.Services;

/// <summary>
/// Anzeige-Helfer für Ordnerlisten: erzeugt möglichst kurze, aber eindeutige
/// Titel. Tragen mehrere Ordner denselben Namen, werden so lange übergeordnete
/// Verzeichnisse vorangestellt, bis die Titel eindeutig sind
/// (z. B. <c>Steuer\2025</c> vs. <c>Privat\2025</c>).
/// </summary>
public static class FolderDisplay
{
    private static readonly char[] Separators = { '/', '\\' };

    /// <summary>
    /// Bildet jeden Pfad auf seinen kürzest-eindeutigen Anzeigetitel ab.
    /// Pfade, die sich nicht weiter unterscheiden lassen (gleicher voller Pfad),
    /// erhalten denselben Titel.
    /// </summary>
    public static Dictionary<string, string> DisambiguateTitles(IEnumerable<string> paths)
    {
        var list = paths.Distinct().ToList();
        var segments = list.ToDictionary(p => p, p => p.Split(Separators, StringSplitOptions.RemoveEmptyEntries));
        var depth = list.ToDictionary(p => p, _ => 1);

        string Title(string p)
        {
            var s = segments[p];
            if (s.Length == 0) return p; // Fallback (z. B. nur "/")
            int d = Math.Min(depth[p], s.Length);
            return string.Join(Path.DirectorySeparatorChar, s[^d..]);
        }

        bool changed = true;
        while (changed)
        {
            changed = false;
            // Pfade, deren Titel noch mit einem anderen kollidiert, um eine
            // Ebene erweitern — sofern sie noch übergeordnete Segmente haben.
            foreach (var group in list.GroupBy(Title).Where(g => g.Count() > 1))
                foreach (var p in group)
                    if (depth[p] < segments[p].Length)
                    {
                        depth[p]++;
                        changed = true;
                    }
        }

        return list.ToDictionary(p => p, Title);
    }
}
