using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using KroModIx.Plugin.CaptainOfIndustry.Services;
using KroModIx.Plugin.Contracts;
using KroModIx.Plugin.TestKit;
using Xunit;

namespace KroModIx.Plugin.CaptainOfIndustry.Tests;

/// <summary>Der GitHub-Weg der Update-Discovery war bis v0.6.0
/// <b>ungetestet</b> — nicht aus Nachlässigkeit, sondern weil es keine
/// <c>IHostServices</c>-Attrappe gab und alle zwölf Pflichtglieder
/// nachzubauen sich für einen Test nicht lohnte. Mit
/// <see cref="FakeHostServices"/> aus dem TestKit (Host v1.33.0) geht es.
///
/// <para>Das Netz wird dabei nie angefasst: die Quellen-Liste kommt aus dem
/// Zwischenspeicher im Plugin-Cache-Ordner (6h-Frist, wird hier frisch
/// angelegt), die Ausgaben aus <see cref="FakeGitHubService"/>.</para></summary>
public sealed class UpdateCheckerGitHubTests : IDisposable
{
    private readonly string _tmp;
    private readonly string _modsDir;
    private readonly DetectedGame _game;
    private readonly FakeHostServices _host;
    private readonly FakeGitHubService _gh = new();
    private readonly CoiUpdateChecker _checker;

    public UpdateCheckerGitHubTests()
    {
        _tmp = Directory.CreateTempSubdirectory("kromodix-coi-upd").FullName;
        var userData = Path.Combine(_tmp, "userdata");
        _modsDir = Path.Combine(userData, "Mods");
        Directory.CreateDirectory(_modsDir);

        _game = new DetectedGame(
            Target: new GameTarget("captain-of-industry", "Captain of Industry", 1594320,
                Array.Empty<string>(), Platforms.Both),
            InstallDir: Path.Combine(_tmp, "game"),
            UserDataDir: userData,
            ProtonPrefix: null,
            Runtime: RuntimeKind.Native,
            Source: GameSource.Steam);
        Directory.CreateDirectory(_game.InstallDir);

        _host = new FakeHostServices(_tmp) { GitHub = _gh };
        SeedSources(("Keranik/COI-Extended", "COI-Extended"));

        var paths = new CoiPathResolver();
        _checker = new CoiUpdateChecker(new CoiModScanner(paths),
            new CoiSourcesService(_host), _host);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tmp, recursive: true); } catch { /* Aufräumen darf scheitern */ }
    }

    /// <summary>Schreibt die Quellen-Liste in den Zwischenspeicher, damit
    /// <c>CoiSourcesService</c> nicht ins Netz greift.</summary>
    private void SeedSources(params (string Repo, string Name)[] sources)
    {
        var index = new CoiSourcesIndex(1, DateTime.UtcNow,
            Array.ConvertAll(sources, s => new CoiSourceEntry(s.Repo, s.Name, "Testeintrag")));
        File.WriteAllText(Path.Combine(_host.PluginCacheDir, "sources-cache.json"),
            JsonSerializer.Serialize(index));
    }

    private void InstallMod(string folder, string version)
    {
        var dir = Path.Combine(_modsDir, folder);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "mod.json"),
            $"{{\"name\":\"{folder}\",\"version\":\"{version}\"}}");
    }

    [Fact]
    public async Task Neueres_Release_wird_als_Update_gemeldet()
    {
        InstallMod("COI-Extended", "1.0.0");
        _gh.AddRelease("Keranik/COI-Extended", "1.2.0");

        var n = await _checker.CheckAsync(_game, force: true,
            TestContext.Current.CancellationToken);

        n.Should().Be(1);
        var c = _checker.Pending.Should().ContainSingle().Subject;
        c.InstalledVersion.Should().Be("1.0.0");
        c.LatestVersion.Should().Be("1.2.0");
        c.Repo.Should().Be("Keranik/COI-Extended");
        _gh.Queries.Should().ContainSingle("ein Repo, eine Abfrage");
    }

    [Fact]
    public async Task Gleiche_Version_meldet_kein_Update()
    {
        InstallMod("COI-Extended", "1.2.0");
        _gh.AddRelease("Keranik/COI-Extended", "1.2.0");

        (await _checker.CheckAsync(_game, force: true, TestContext.Current.CancellationToken))
            .Should().Be(0);
        _checker.Pending.Should().BeEmpty();
    }

    /// <summary>Was keinem Source-Eintrag zugeordnet werden kann, meldet
    /// kein Update statt zu raten — und kostet dann auch keine
    /// GitHub-Abfrage.</summary>
    [Fact]
    public async Task Unbekannter_Mod_kostet_keine_Abfrage()
    {
        InstallMod("IrgendeinFremderMod", "1.0.0");

        (await _checker.CheckAsync(_game, force: true, TestContext.Current.CancellationToken))
            .Should().Be(0);
        _gh.Queries.Should().BeEmpty();
    }

    /// <summary>Der Zweig, der im Alltag fast nie läuft und deshalb der ist,
    /// der unbemerkt kaputtgeht: greift die Raten-Sperre, liefert der
    /// Baukasten die Ausgabe über den Umleitungs-Pfad — Tag bekannt,
    /// Dateiliste leer. Für diese Prüfung reicht der Tag, also muss sie
    /// trotzdem funktionieren.
    ///
    /// <para>Vorher entdeckte der Prüfer das Limit für <b>jedes Repo
    /// neu</b>: eine verbrannte API-Anfrage plus einen Umleitungs-Aufruf,
    /// Mod für Mod. Die Sperre des Host-Baukastens gilt für alle Aufrufer
    /// gemeinsam.</para></summary>
    [Fact]
    public async Task Bei_Raten_Sperre_laeuft_die_Pruefung_weiter()
    {
        InstallMod("COI-Extended", "1.0.0");
        _gh.AddRelease("Keranik/COI-Extended", "1.2.0");
        _gh.RateLimited = true;

        var n = await _checker.CheckAsync(_game, force: true,
            TestContext.Current.CancellationToken);

        n.Should().Be(1, "der Umleitungs-Pfad kennt den Tag, und mehr braucht es hier nicht");
        _checker.Pending.Should().ContainSingle()
            .Which.LatestVersion.Should().Be("1.2.0");
    }

    /// <summary>Ohne Release passiert nichts — und vor allem wird nichts
    /// erfunden.</summary>
    [Fact]
    public async Task Repo_ohne_Release_meldet_nichts()
    {
        InstallMod("COI-Extended", "1.0.0");

        (await _checker.CheckAsync(_game, force: true, TestContext.Current.CancellationToken))
            .Should().Be(0);
        _gh.Queries.Should().ContainSingle("gefragt wurde, geantwortet hat niemand");
    }

    /// <summary>Ein Mod ohne Versionsangabe in der <c>mod.json</c> lässt
    /// sich nicht vergleichen — dann keine Abfrage, kein Raten.</summary>
    [Fact]
    public async Task Mod_ohne_Version_kostet_keine_Abfrage()
    {
        var dir = Path.Combine(_modsDir, "COI-Extended");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "mod.json"), """{"name":"COI-Extended"}""");

        (await _checker.CheckAsync(_game, force: true, TestContext.Current.CancellationToken))
            .Should().Be(0);
        _gh.Queries.Should().BeEmpty();
    }

    /// <summary>Die 6h-Frist auf dem Ergebnis: ein zweiter Aufruf ohne
    /// <c>force</c> fragt GitHub nicht erneut.</summary>
    [Fact]
    public async Task Zweiter_Aufruf_innerhalb_der_Frist_fragt_nicht_erneut()
    {
        InstallMod("COI-Extended", "1.0.0");
        _gh.AddRelease("Keranik/COI-Extended", "1.2.0");

        await _checker.CheckAsync(_game, force: true, TestContext.Current.CancellationToken);
        _gh.Queries.Should().HaveCount(1);

        await _checker.CheckAsync(_game, force: false, TestContext.Current.CancellationToken);
        _gh.Queries.Should().HaveCount(1, "die Frist läuft noch");
    }
}
