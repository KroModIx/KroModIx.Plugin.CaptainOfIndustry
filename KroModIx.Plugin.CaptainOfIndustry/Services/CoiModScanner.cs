using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using KroModIx.Plugin.Contracts;

namespace KroModIx.Plugin.CaptainOfIndustry.Services;

/// <summary>Enumeriert die CoI-Mods im primaeren Mods-Ordner (via
/// <see cref="CoiPathResolver"/>). Ein Mod ist ein Ordner mit <c>mod.json</c>
/// im Root. <c>.disabled</c>-Suffix am Ordner-Namen = deaktiviert.
/// Fehlt mod.json, wird der Ordner-Name als DisplayName genutzt — nicht
/// verschluckt, damit der User den Mod deinstallieren kann.</summary>
public sealed class CoiModScanner
{
    private readonly CoiPathResolver _paths;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public CoiModScanner(CoiPathResolver paths) => _paths = paths;

    public IReadOnlyList<CoiMod> Scan(DetectedGame game)
    {
        var modsDir = _paths.GetModsDir(game);
        if (!Directory.Exists(modsDir)) return Array.Empty<CoiMod>();

        var mods = new List<CoiMod>();
        foreach (var dir in Directory.EnumerateDirectories(modsDir))
        {
            try
            {
                var folderRaw = Path.GetFileName(dir);
                var isEnabled = !folderRaw.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase);
                var displayFolder = isEnabled ? folderRaw : folderRaw[..^".disabled".Length];

                var meta = ParseModJson(Path.Combine(dir, "mod.json"), displayFolder);
                long size = 0;
                try
                {
                    foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                        try { size += new FileInfo(f).Length; } catch { }
                }
                catch { }

                mods.Add(new CoiMod(
                    Path: dir,
                    FolderName: displayFolder,
                    DisplayName: meta.Name,
                    Author: meta.Author,
                    Version: meta.Version,
                    Description: meta.Description,
                    IsEnabled: isEnabled,
                    SizeBytes: size,
                    InstalledUtc: new DirectoryInfo(dir).CreationTimeUtc));
            }
            catch { }
        }
        // Fremdverwaltete Eintraege einmal beim Scan markieren.
        return mods.Select(m => m.MitVerwalterErkennung())
            .OrderBy(m => m.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Parst mod.json. Toleriert die ueblichen Feld-Varianten
    /// (name/Name, author/Author, version/Version, description/Description).
    /// Fehler = Fallback auf Ordner-Namen als DisplayName + null fuer Rest.</summary>
    public static (string Name, string? Author, string? Version, string? Description)
        ParseModJson(string path, string fallbackName)
    {
        if (!File.Exists(path)) return (fallbackName, null, null, null);
        try
        {
            var text = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            string? Read(params string[] keys)
            {
                foreach (var k in keys)
                {
                    if (root.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String)
                    {
                        var s = v.GetString();
                        if (!string.IsNullOrWhiteSpace(s)) return s.Trim();
                    }
                }
                return null;
            }
            var name = Read("name", "Name", "displayName", "DisplayName") ?? fallbackName;
            var author = Read("author", "Author");
            var version = Read("version", "Version");
            var desc = Read("description", "Description");
            return (name, author, version, desc);
        }
        catch
        {
            return (fallbackName, null, null, null);
        }
    }
}
