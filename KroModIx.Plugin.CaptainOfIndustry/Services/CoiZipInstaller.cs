using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KroModIx.Plugin.Contracts;
using NLog;

namespace KroModIx.Plugin.CaptainOfIndustry.Services;

/// <summary>Installiert ein Mod-Archiv in den CoI-Mods-Ordner. Auto-Layout-
/// Detection:
/// <list type="bullet">
/// <item>Archiv enthaelt <c>mod.json</c> im Root → wird als eigener Mod-
/// Ordner extrahiert (Ordner-Name aus Archiv-Filename).</item>
/// <item>Archiv enthaelt <c>&lt;X&gt;/mod.json</c> in einem Root-Ordner →
/// dieser Ordner wird als Mod-Ordner nach Mods/ extrahiert.</item>
/// <item>Kein mod.json → Fehler „Kein CoI-Mod erkennbar".</item>
/// </list>
///
/// <para><b>Seit v0.6.0 über <c>IHostServices.Archives</c></b> (Host
/// v1.32.0), und damit <b>auch RAR und 7z</b>. Vorher war das auf ZIP
/// begrenzt, mit der Begründung, SharpCompress lohne sich für einen reinen
/// Workshop-Consumer nicht im Bundle — der Host bringt es mit, die
/// Begründung ist entfallen.</para>
///
/// <para><b>Der eigene Ausbruch-Schutz prüfte <c>Contains("..")</c>.</b> Das
/// lässt einen absoluten Eintragsnamen durch, und <c>Path.Combine</c>
/// verwirft dann das Zielverzeichnis. Am Cyberpunk-Installer, der dieselbe
/// Prüfung trug, am 03.10.2026 nachgewiesen: die Datei landete außerhalb des
/// Spiels, und der Install meldete Erfolg. Der Mod-Ordner je
/// <c>mod.json</c> entsteht jetzt über
/// <see cref="ArchiveExtractOptions.StripPrefix"/> statt über eigene
/// Pfad-Arithmetik.</para></summary>
public sealed class CoiZipInstaller
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    private readonly IArchiveService _archives;
    private readonly CoiPathResolver _paths;

    public CoiZipInstaller(IArchiveService archives, CoiPathResolver paths)
    {
        _archives = archives;
        _paths = paths;
    }

    /// <summary>Ziel-Verzeichnis des Installs — fuer Backup-Snapshots vor dem
    /// Schreiben. Legt nichts an.</summary>
    public string GetModsDir(DetectedGame game) => _paths.GetModsDir(game);

    /// <summary>Endungs-Vorfilter fuer den Downloads-Tab. Kommt aus dem
    /// Host-Baukasten, damit ein dort neu unterstuetztes Format nicht in
    /// neun Plugins nachgetragen werden muss.</summary>
    public IReadOnlyList<string> SupportedExtensions => _archives.SupportedExtensions;

    public bool HasSupportedExtension(string path) => _archives.HasSupportedExtension(path);

    public CoiZipInstallResult Install(string archivePath, DetectedGame game)
    {
        if (!File.Exists(archivePath))
            return CoiZipInstallResult.Fail($"Archiv nicht gefunden: {archivePath}");

        // Am Inhalt pruefen, nicht an der Endung: ein Download mit falscher
        // Endung landete sonst unveraendert im Spiel.
        if (_archives.DetectKind(archivePath) == ArchiveKind.Unknown)
            return CoiZipInstallResult.Fail(
                "Das ist kein lesbares Archiv (ZIP/RAR/7z) — eventuell ein abgebrochener Download.");

        var modsDir = _paths.EnsureModsDir(game);

        try
        {
            var entries = _archives.List(archivePath);
            if (entries.Count == 0)
                return CoiZipInstallResult.Fail("Archiv ist leer.");

            // Kandidaten fuer mod.json finden (root oder ein Root-Ordner).
            var modJsonEntries = entries.Where(e =>
                e.Path.EndsWith("/mod.json", StringComparison.OrdinalIgnoreCase)
                || string.Equals(e.Path, "mod.json", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (modJsonEntries.Count == 0)
                return CoiZipInstallResult.Fail(
                    "Archiv enthaelt keine mod.json — kein CoI-Mod erkennbar.");

            var installedFiles = new List<string>();
            var modFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var abgelehnt = new List<string>();

            foreach (var mj in modJsonEntries)
            {
                string? prefix;
                string folderName;
                var slash = mj.Path.LastIndexOf('/');
                if (slash < 0)
                {
                    // mod.json im Archiv-Root → Ordner-Name aus Archiv-Filename.
                    prefix = null;
                    folderName = SanitizeFolder(Path.GetFileNameWithoutExtension(archivePath));
                }
                else
                {
                    prefix = mj.Path[..slash];
                    folderName = new DirectoryInfo(prefix).Name;
                }
                var target = Path.Combine(modsDir, folderName);
                Directory.CreateDirectory(target);
                modFolders.Add(target);

                var r = _archives.Extract(archivePath, target,
                    new ArchiveExtractOptions(StripPrefix: prefix));
                installedFiles.AddRange(r.ExtractedPaths);
                abgelehnt.AddRange(r.SkippedUnsafe);
            }

            // Ein Ausbruchsversuch bricht den Install ab, statt still das zu
            // installieren was durchkam. Das trifft auch ein bloss kaputtes
            // Archiv mit einem krummen Eintrag unter zweihundert; bewusst,
            // denn ein Archiv das aus dem Mods-Ordner herausschreiben will
            // ist nicht "ueberwiegend in Ordnung", und die Entscheidung
            // gehoert dem Nutzer.
            if (abgelehnt.Count > 0)
            {
                Log.Warn("Ausbruchsversuch im Archiv, {Count} Eintrag/Einträge abgelehnt: {Entries}",
                    abgelehnt.Count, string.Join(", ", abgelehnt));
                return new CoiZipInstallResult(false,
                    $"Abgebrochen: {abgelehnt.Count} Eintrag/Einträge wollten aus dem "
                    + "Mods-Ordner herausschreiben — "
                    + string.Join(", ", abgelehnt.Take(3))
                    + (abgelehnt.Count > 3 ? ", …" : "")
                    + $". {installedFiles.Count} Datei(en) waren schon geschrieben, "
                    + "bevor das auffiel.",
                    installedFiles, modFolders.ToList());
            }

            return CoiZipInstallResult.Ok(
                $"{modFolders.Count} Mod-Ordner extrahiert ({installedFiles.Count} Datei(en)).",
                installedFiles, modFolders.ToList());
        }
        catch (Exception ex)
        {
            Log.Warn(ex, "Install fehlgeschlagen: {Archive}", archivePath);
            return CoiZipInstallResult.Fail($"Fehler: {ex.Message}");
        }
    }

    private static string SanitizeFolder(string s)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(s.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
    }
}

public sealed record CoiZipInstallResult(
    bool Success,
    string Message,
    IReadOnlyList<string> InstalledPaths,
    IReadOnlyList<string> ModFolderPaths)
{
    public static CoiZipInstallResult Ok(string msg,
        IReadOnlyList<string> paths, IReadOnlyList<string> folders) =>
        new(true, msg, paths, folders);
    public static CoiZipInstallResult Fail(string msg) =>
        new(false, msg, Array.Empty<string>(), Array.Empty<string>());
}
