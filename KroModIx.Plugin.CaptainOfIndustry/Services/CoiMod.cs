using System;

namespace KroModIx.Plugin.CaptainOfIndustry.Services;

/// <summary>Ein installierter Captain-of-Industry-Mod. Ordner unter dem
/// User-Docs-Mods-Verzeichnis mit <c>mod.json</c> im Root. Toggle via
/// <c>.disabled</c>-Suffix am Ordner-Namen (CoI ignoriert Ordner mit
/// unbekannter Extension). Wenn mod.json fehlt oder unlesbar ist,
/// Fallback auf Ordner-Namen als DisplayName — Mod wird trotzdem
/// gelistet damit der User ihn deinstallieren kann.</summary>
public sealed record CoiMod(
    string Path,
    string FolderName,
    string DisplayName,
    string? Author,
    string? Version,
    string? Description,
    bool IsEnabled,
    long SizeBytes,
    DateTime InstalledUtc);
