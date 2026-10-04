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
    DateTime InstalledUtc)
{
    /// <summary>v0.7.0: gesetzt, wenn dieser Eintrag einem <b>anderen
    /// Mod-Manager</b> gehört (lmm, r2modman, Vortex, SMM/ficsit). Erkannt am
    /// Verweis, nicht am Namen — siehe
    /// <see cref="KroModIx.Plugin.Contracts.ForeignManagerDetection"/>.
    /// Solche Einträge werden gelistet, damit der Nutzer sieht was im Spiel
    /// liegt, aber nicht verändert: am 04.10.2026 hat ein
    /// Deinstallieren-Klick im Icarus-Plugin lmms Pak entfernt und damit
    /// lautlos eine Mod aus dem Spiel genommen, während ihre Quelle woanders
    /// unversehrt lag.</summary>
    public string? ManagedBy { get; init; }

    /// <summary>Ob das Plugin diesen Eintrag verändern darf.</summary>
    public bool CanModify => ManagedBy is null;

    /// <summary>Einmalige Erkennung beim Scan — der Scanner legt sie über
    /// seine Ergebnisliste. Als Methode und nicht als berechnete Eigenschaft,
    /// weil sonst jede Bindung in der Oberfläche einen Dateisystem-Zugriff
    /// auslöst.</summary>
    public CoiMod MitVerwalterErkennung()
        => KroModIx.Plugin.Contracts.ForeignManagerDetection.IsForeignManaged(Path, out var wer)
            ? this with { ManagedBy = wer }
            : this;
}

