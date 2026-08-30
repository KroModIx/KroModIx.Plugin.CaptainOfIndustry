using System;
using System.Collections.Generic;
using KroModIx.Plugin.CaptainOfIndustry.Services;
using Xunit;

namespace KroModIx.Plugin.CaptainOfIndustry.Tests;

/// <summary>Namens-Matching installierter Mod → Source-Eintrag. Das ist der
/// wackelige Teil der Update-Discovery: CoI-Mod-Ordner heissen selten exakt
/// wie das GitHub-Repo.</summary>
public sealed class UpdateCheckerTests
{
    private static readonly IReadOnlyList<CoiSourceEntry> Sources =
    [
        new CoiSourceEntry("Keranik/COI-Extended", "COI-Extended", "Bundle"),
    ];

    private static CoiMod Mod(string displayName, string folderName, string? version = "1.0") =>
        new(Path: "/tmp/" + folderName, FolderName: folderName, DisplayName: displayName,
            Author: null, Version: version, Description: null, IsEnabled: true,
            SizeBytes: 0, InstalledUtc: DateTime.UtcNow);

    [Theory]
    [InlineData("COI-Extended", "COI-Extended")]
    [InlineData("COI Extended", "coi_extended")]
    [InlineData("coiextended", "whatever")]
    [InlineData("COI-Extended Buildings", "COI-Extended")]
    public void Findet_passende_Source(string displayName, string folderName)
        => Assert.NotNull(CoiUpdateChecker.MatchSource(Mod(displayName, folderName), Sources));

    [Theory]
    [InlineData("Irgendein Fremdmod", "fremdmod")]
    [InlineData("", "")]
    [InlineData("ab", "ab")]  // zu kurz fuer sinnvolles Matching
    public void Kein_Treffer_wird_nicht_geraten(string displayName, string folderName)
        => Assert.Null(CoiUpdateChecker.MatchSource(Mod(displayName, folderName), Sources));
}
