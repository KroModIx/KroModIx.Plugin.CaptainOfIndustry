using System;
using System.IO;
using KroModIx.Plugin.Contracts;

namespace KroModIx.Plugin.CaptainOfIndustry.Services;

/// <summary>Loest den <c>Mods/</c>-Ordner fuer Captain of Industry. Fallback-
/// Chain je nach Runtime:
/// <list type="bullet">
/// <item><c>&lt;UserDataDir&gt;/Mods/</c> — vom Host geliefert, Proton-aware
/// (Host mappt bereits <c>&lt;prefix&gt;/drive_c/users/steamuser/My Documents/
/// Captain of Industry/</c> auf UserDataDir wenn vorhanden).</item>
/// <item><c>&lt;ProtonPrefix&gt;/drive_c/users/steamuser/My Documents/Captain of Industry/Mods/</c>
/// — Proton-Fallback wenn UserDataDir nicht gefuellt (Wine nutzt „My Documents"
/// als XP-Style-Ordner, nicht „Documents").</item>
/// <item><c>~/Documents/Captain of Industry/Mods/</c> — Native (Windows +
/// Linux Native-Builds).</item>
/// <item><c>&lt;InstallDir&gt;/Mods/</c> — allerletzter Fallback (manche
/// Modder legen es dorthin, funktioniert aber offiziell nicht).</item>
/// </list>
/// Der erste existierende Kandidat wird zurueckgegeben. Bei Erst-Install
/// wird die primaere Wahl (UserDataDir bevorzugt) angelegt.</summary>
public sealed class CoiPathResolver
{
    /// <summary>Findet den effektiven Mods-Ordner (kann leer sein wenn noch
    /// nichts existiert). Legt keinen Ordner an — dafuer <see cref="EnsureModsDir"/>.</summary>
    public string GetModsDir(DetectedGame game)
    {
        foreach (var candidate in EnumerateCandidates(game))
        {
            if (Directory.Exists(candidate)) return candidate;
        }
        // Nichts da → primaeren Kandidaten als Default-Anzeige-Pfad
        return PrimaryCandidate(game);
    }

    /// <summary>Sicherstellen dass der primaere Kandidat existiert.
    /// Erst-Install-Weg fuer den ZipInstaller.</summary>
    public string EnsureModsDir(DetectedGame game)
    {
        var dir = PrimaryCandidate(game);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string PrimaryCandidate(DetectedGame game)
    {
        // UserDataDir gewinnt wenn der Host es ableiten konnte.
        if (!string.IsNullOrEmpty(game.UserDataDir))
            return Path.Combine(game.UserDataDir, "Mods");
        // Proton-Prefix als naechster Kandidat.
        if (!string.IsNullOrEmpty(game.ProtonPrefix))
            return Path.Combine(game.ProtonPrefix,
                "drive_c", "users", "steamuser", "My Documents",
                "Captain of Industry", "Mods");
        // Native Documents-Ordner.
        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (!string.IsNullOrEmpty(docs))
            return Path.Combine(docs, "Captain of Industry", "Mods");
        // Absoluter Fallback im InstallDir.
        return Path.Combine(game.InstallDir, "Mods");
    }

    private static System.Collections.Generic.IEnumerable<string> EnumerateCandidates(DetectedGame game)
    {
        if (!string.IsNullOrEmpty(game.UserDataDir))
            yield return Path.Combine(game.UserDataDir, "Mods");
        if (!string.IsNullOrEmpty(game.ProtonPrefix))
        {
            yield return Path.Combine(game.ProtonPrefix,
                "drive_c", "users", "steamuser", "My Documents",
                "Captain of Industry", "Mods");
            // Manche Proton-Prefixe schreiben tatsaechlich „Documents".
            yield return Path.Combine(game.ProtonPrefix,
                "drive_c", "users", "steamuser", "Documents",
                "Captain of Industry", "Mods");
        }
        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (!string.IsNullOrEmpty(docs))
            yield return Path.Combine(docs, "Captain of Industry", "Mods");
        yield return Path.Combine(game.InstallDir, "Mods");
    }
}
