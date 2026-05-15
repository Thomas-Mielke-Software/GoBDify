using GoBDify.Core;

namespace GoBDify.Cli;

internal static class CliRunner
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || args.Contains("-h") || args.Contains("--help"))
        {
            PrintUsage();
            return args.Length == 0 ? 1 : 0;
        }

        if (args[0].Equals("config", StringComparison.OrdinalIgnoreCase))
            return ConfigCommand(args.Skip(1).ToArray());

        bool auditOnly = false;
        string? folder = null;
        string? settingsPath = null;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--audit":
                case "-a":
                    auditOnly = true;
                    break;
                case "--timestamp":
                case "-t":
                    auditOnly = false;
                    break;
                case "--config":
                case "-c":
                    if (i + 1 < args.Length) settingsPath = args[++i];
                    break;
                default:
                    if (folder == null) folder = args[i];
                    else { Console.Error.WriteLine($"Unbekanntes Argument: {args[i]}"); return 2; }
                    break;
            }
        }

        if (folder == null)
        {
            Console.Error.WriteLine("Fehler: Ordner fehlt.");
            PrintUsage();
            return 2;
        }
        if (!Directory.Exists(folder))
        {
            Console.Error.WriteLine($"Ordner existiert nicht: {folder}");
            return 2;
        }

        var settings = AppSettingsStore.Load(settingsPath);
        var validation = settings.Validate();
        if (validation != null)
        {
            Console.Error.WriteLine($"Konfigurationsfehler: {validation}");
            return 3;
        }

        var processor = new ChainProcessor();
        var progress = new Progress<ProgressInfo>(p => Console.Error.WriteLine($"  [{p.Fraction,5:P0}] {p.Message}"));

        if (auditOnly)
        {
            var audit = await processor.AuditAsync(folder, progress);
            PrintAudit(audit);
            return audit.Error != null ? 4 : (HasIssues(audit) ? 5 : 0);
        }
        else
        {
            var result = await processor.AuditAndTimestampAsync(folder, settings, progress);
            PrintAudit(result.Audit);
            if (result.Audit.Error != null)
            {
                Console.Error.WriteLine($"FEHLER: {result.Audit.Error}");
                return 4;
            }
            if (result.NewChainNumber.HasValue)
            {
                Console.WriteLine();
                Console.WriteLine($"NEUER TIMESTAMP: timestamp{result.NewChainNumber.Value:D5}.sha256");
                foreach (var t in result.NewTimestamps ?? new List<TimestampTokenInfo>())
                    Console.WriteLine($"  -> {t.TstFileName}  ({t.IssuerName}, {t.Timestamp:u})");
            }
            else if (result.Audit.NewFiles.Count == 0)
            {
                Console.WriteLine();
                Console.WriteLine("KEINE NEUEN DATEIEN. Kein neuer Timestamp.");
            }
            return HasIssues(result.Audit) ? 5 : 0;
        }
    }

    private static bool HasIssues(AuditReport a) =>
        a.Chain.Any(c =>
            c.Files.Any(f => f.Status is FileVerificationStatus.Modified or FileVerificationStatus.Missing)
            || c.Timestamps.Any(t => t.Status != TimestampStatus.Valid));

    private static void PrintAudit(AuditReport a)
    {
        Console.WriteLine($"Ordner: {a.FolderPath}");
        foreach (var c in a.Chain)
        {
            Console.WriteLine();
            Console.WriteLine($"=== {c.Sha256FileName} ===");
            foreach (var t in c.Timestamps)
            {
                var status = t.Status switch
                {
                    TimestampStatus.Valid => "OK",
                    TimestampStatus.Invalid => "UNGÜLTIG",
                    _ => "UNLESBAR",
                };
                Console.WriteLine($"  Timestamp {t.TstFileName}: {status}  {t.Timestamp:u}  {t.IssuerName}{(t.Error != null ? $"  ({t.Error})" : "")}");
            }
            foreach (var f in c.Files)
            {
                string tag = f.Status switch
                {
                    FileVerificationStatus.Ok => "OK      ",
                    FileVerificationStatus.Modified => "VERÄNDERT",
                    FileVerificationStatus.Missing => "FEHLT   ",
                    _ => "        ",
                };
                Console.WriteLine($"    {tag}  {f.FileName}");
            }
        }
        if (a.NewFiles.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Nicht zugeordnete (neue) Dateien:");
            foreach (var f in a.NewFiles) Console.WriteLine($"    NEU       {f}");
        }
    }

    private static int ConfigCommand(string[] args)
    {
        var settings = AppSettingsStore.Load();
        if (args.Length == 0 || args[0] == "show")
        {
            Console.WriteLine($"Settings: {AppSettingsStore.DefaultPath}");
            Console.WriteLine($"  Paranoia-Modus: {settings.ParanoiaMode}");
            Console.WriteLine($"  TSAs:           {string.Join(", ", settings.SelectedTsaIds)}");
            Console.WriteLine();
            Console.WriteLine("Verfügbare TSAs:");
            foreach (var t in TimestampAuthorities.Defaults)
                Console.WriteLine($"  {t.Id,-12}  {t.Name,-12}  {t.Url}");
            return 0;
        }
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--set-tsa":
                    if (i + 1 >= args.Length) return Fail("--set-tsa erwartet kommaseparierte IDs");
                    settings.SelectedTsaIds = args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                    break;
                case "--paranoia":
                    if (i + 1 >= args.Length) return Fail("--paranoia erwartet on|off");
                    settings.ParanoiaMode = args[++i].Equals("on", StringComparison.OrdinalIgnoreCase);
                    break;
                default:
                    return Fail($"Unbekanntes config-Argument: {args[i]}");
            }
        }
        var v = settings.Validate();
        if (v != null) { Console.Error.WriteLine($"Warnung: {v}"); }
        AppSettingsStore.Save(settings);
        Console.WriteLine("Gespeichert.");
        return 0;

        static int Fail(string m) { Console.Error.WriteLine(m); return 2; }
    }

    private static void PrintUsage()
    {
        Console.WriteLine("gobdify — Hash-Kette mit RFC3161-Timestamp für einen Ordner");
        Console.WriteLine();
        Console.WriteLine("Verwendung:");
        Console.WriteLine("  gobdify <ordner> [--audit | --timestamp] [--config <pfad>]");
        Console.WriteLine("  gobdify config [show]");
        Console.WriteLine("  gobdify config --set-tsa certum,digicert,freetsa --paranoia on|off");
        Console.WriteLine();
        Console.WriteLine("Optionen:");
        Console.WriteLine("  -a, --audit        Nur prüfen, keine neuen Timestamps erstellen");
        Console.WriteLine("  -t, --timestamp    Prüfen UND neue Dateien timestampen (Standard)");
        Console.WriteLine("  -c, --config PFAD  Alternative Settings-Datei");
        Console.WriteLine();
        Console.WriteLine("Exit-Codes: 0=ok, 2=Argumentfehler, 3=Konfig, 4=Fehler, 5=Audit-Issues");
    }
}
