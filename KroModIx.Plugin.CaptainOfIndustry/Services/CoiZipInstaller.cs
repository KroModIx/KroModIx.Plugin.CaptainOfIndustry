using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using KroModIx.Plugin.Contracts;
using NLog;

namespace KroModIx.Plugin.CaptainOfIndustry.Services;

/// <summary>Installiert ein .zip-Archiv in den CoI-Mods-Ordner. Auto-Layout-
/// Detection:
/// <list type="bullet">
/// <item>Archiv enthaelt <c>mod.json</c> im Root → wird als eigener Mod-
/// Ordner extrahiert (Ordner-Name aus Archiv-Filename).</item>
/// <item>Archiv enthaelt <c>&lt;X&gt;/mod.json</c> in einem Root-Ordner →
/// dieser Ordner wird als Mod-Ordner nach Mods/ extrahiert.</item>
/// <item>Kein mod.json → Fehler „Kein CoI-Mod erkennbar".</item>
/// </list>
/// Nur .zip — RAR/7z brauchen SharpCompress, das bringt der reine
/// Workshop-Consumer sonst nicht ins Bundle. Kann in v0.3+ dazukommen
/// wenn ein User es braucht.</summary>
public sealed class CoiZipInstaller
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    private readonly CoiPathResolver _paths;

    public CoiZipInstaller(CoiPathResolver paths) => _paths = paths;

    /// <summary>Ziel-Verzeichnis des Installs — fuer Backup-Snapshots vor dem
    /// Schreiben. Legt nichts an.</summary>
    public string GetModsDir(DetectedGame game) => _paths.GetModsDir(game);

    public CoiZipInstallResult Install(string archivePath, DetectedGame game)
    {
        if (!File.Exists(archivePath))
            return CoiZipInstallResult.Fail($"Archiv nicht gefunden: {archivePath}");
        var ext = Path.GetExtension(archivePath).ToLowerInvariant();
        if (ext != ".zip")
            return CoiZipInstallResult.Fail(
                $"Nur .zip unterstuetzt (dieser Datei-Typ: {ext}).");

        var modsDir = _paths.EnsureModsDir(game);

        try
        {
            using var zip = ZipFile.OpenRead(archivePath);
            var entries = zip.Entries.Where(e => !string.IsNullOrEmpty(e.FullName)).ToList();
            if (entries.Count == 0)
                return CoiZipInstallResult.Fail("Archiv ist leer.");

            // Kandidaten fuer mod.json finden (root oder ein Root-Ordner).
            var modJsonEntries = entries.Where(e =>
                e.FullName.EndsWith("/mod.json", StringComparison.OrdinalIgnoreCase)
                || string.Equals(e.FullName, "mod.json", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (modJsonEntries.Count == 0)
                return CoiZipInstallResult.Fail(
                    "Archiv enthaelt keine mod.json — kein CoI-Mod erkennbar.");

            var installedFiles = new List<string>();
            var modFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var mj in modJsonEntries)
            {
                string prefix, folderName;
                var slash = mj.FullName.LastIndexOf('/');
                if (slash < 0)
                {
                    // mod.json im Archiv-Root → Ordner-Name aus Archiv-Filename.
                    prefix = "";
                    folderName = SanitizeFolder(Path.GetFileNameWithoutExtension(archivePath));
                }
                else
                {
                    prefix = mj.FullName.Substring(0, slash + 1);
                    folderName = new DirectoryInfo(prefix.TrimEnd('/')).Name;
                }
                var target = Path.Combine(modsDir, folderName);
                Directory.CreateDirectory(target);
                modFolders.Add(target);

                foreach (var e in entries.Where(x => x.FullName.StartsWith(prefix,
                    StringComparison.OrdinalIgnoreCase)))
                {
                    var rel = prefix.Length == 0 ? e.FullName : e.FullName.Substring(prefix.Length);
                    if (string.IsNullOrEmpty(rel) || rel.EndsWith("/")) continue;
                    if (rel.Contains("..")) { Log.Warn("Zip-Slip: {N}", rel); continue; }
                    var dst = Path.Combine(target, rel.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
                    e.ExtractToFile(dst, overwrite: true);
                    installedFiles.Add(dst);
                }
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
