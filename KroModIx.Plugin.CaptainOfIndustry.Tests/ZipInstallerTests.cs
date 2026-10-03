using System;
using System.IO;
using System.IO.Compression;
using FluentAssertions;
using KroModIx.Plugin.CaptainOfIndustry.Services;
using KroModIx.Plugin.Contracts;
using KroModIx.Plugin.TestKit;
using Xunit;

namespace KroModIx.Plugin.CaptainOfIndustry.Tests;

/// <summary>Der Installer hatte bis v0.6.0 (2026-10-03) <b>keine</b> Tests —
/// und einen eigenen Ausbruch-Schutz, der nur auf <c>..</c> prüfte. Beides
/// hier nachgeholt.</summary>
public sealed class ZipInstallerTests : IDisposable
{
    private readonly string _tmp;
    private readonly string _modsDir;
    private readonly DetectedGame _game;
    private readonly FakeArchiveService _archives = new();
    private readonly CoiZipInstaller _installer;

    public ZipInstallerTests()
    {
        _tmp = Directory.CreateTempSubdirectory("kromodix-coi-zip").FullName;
        var userData = Path.Combine(_tmp, "userdata");
        Directory.CreateDirectory(userData);
        _modsDir = Path.Combine(userData, "Mods");

        _game = new DetectedGame(
            Target: new GameTarget("captain-of-industry", "Captain of Industry", 1594320,
                Array.Empty<string>(), Platforms.Both),
            InstallDir: Path.Combine(_tmp, "game"),
            UserDataDir: userData,
            ProtonPrefix: null,
            Runtime: RuntimeKind.Native,
            Source: GameSource.Steam);
        Directory.CreateDirectory(_game.InstallDir);

        _installer = new CoiZipInstaller(_archives, new CoiPathResolver());
    }

    public void Dispose()
    {
        try { Directory.Delete(_tmp, recursive: true); } catch { /* Aufräumen darf scheitern */ }
    }

    private string BuildZip(string name, params (string Path, string Content)[] entries)
    {
        var zipPath = Path.Combine(_tmp, name);
        if (File.Exists(zipPath)) File.Delete(zipPath);
        using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        foreach (var (path, content) in entries)
        {
            using var s = archive.CreateEntry(path).Open();
            using var sw = new StreamWriter(s);
            sw.Write(content);
        }
        return zipPath;
    }

    private const string ModJson = """{"name":"MeinMod","version":"1.0"}""";

    [Fact]
    public void Mod_json_in_einem_Ordner_wird_dieser_Ordner()
    {
        var zip = BuildZip("download.zip",
            ("CoiExtended/mod.json", ModJson),
            ("CoiExtended/Assets/bild.png", "png"),
            ("liesmich.txt", "Anleitung"));

        var r = _installer.Install(zip, _game);

        r.Success.Should().BeTrue(r.Message);
        File.Exists(Path.Combine(_modsDir, "CoiExtended", "mod.json")).Should().BeTrue();
        File.Exists(Path.Combine(_modsDir, "CoiExtended", "Assets", "bild.png")).Should().BeTrue();
        File.Exists(Path.Combine(_modsDir, "CoiExtended", "liesmich.txt"))
            .Should().BeFalse("was außerhalb des Mod-Ordners liegt, gehört nicht in den Mod");
        r.ModFolderPaths.Should().ContainSingle();
    }

    /// <summary>Liegt die <c>mod.json</c> im Archiv-Wurzelverzeichnis, gibt
    /// es keinen Ordnernamen — dann kommt er aus dem Archiv-Dateinamen.</summary>
    [Fact]
    public void Mod_json_im_Wurzelverzeichnis_nimmt_den_Archivnamen()
    {
        var zip = BuildZip("SuperMod.zip",
            ("mod.json", ModJson),
            ("Assets/x.png", "png"));

        var r = _installer.Install(zip, _game);

        r.Success.Should().BeTrue(r.Message);
        File.Exists(Path.Combine(_modsDir, "SuperMod", "mod.json")).Should().BeTrue();
        File.Exists(Path.Combine(_modsDir, "SuperMod", "Assets", "x.png")).Should().BeTrue();
    }

    [Fact]
    public void Mehrere_mod_json_werden_zu_mehreren_Ordnern()
    {
        var zip = BuildZip("paket.zip",
            ("ModA/mod.json", ModJson),
            ("ModB/mod.json", ModJson),
            ("ModB/Assets/y.png", "png"));

        var r = _installer.Install(zip, _game);

        r.Success.Should().BeTrue(r.Message);
        r.ModFolderPaths.Should().HaveCount(2);
        File.Exists(Path.Combine(_modsDir, "ModA", "mod.json")).Should().BeTrue();
        File.Exists(Path.Combine(_modsDir, "ModB", "Assets", "y.png")).Should().BeTrue();
        File.Exists(Path.Combine(_modsDir, "ModA", "Assets", "y.png"))
            .Should().BeFalse("die Ordner dürfen sich nicht vermischen");
    }

    [Fact]
    public void Ohne_mod_json_schlaegt_es_fehl()
    {
        var zip = BuildZip("fremd.zip", ("irgendwas/datei.txt", "hi"));

        var r = _installer.Install(zip, _game);

        r.Success.Should().BeFalse();
        r.Message.Should().Contain("kein CoI-Mod erkennbar");
    }

    /// <summary>Der Fall, an dem die alte Prüfung vorbeiging — nur im
    /// Wurzel-Zweig prüfbar, denn mit Mod-Ordner hält schon der
    /// <c>StripPrefix</c> den Fremdeintrag heraus.</summary>
    [Fact]
    public void Absoluter_Eintragsname_bricht_nicht_aus()
    {
        var opfer = Path.Combine(_tmp, "ausserhalb.txt");
        var zip = BuildZip("boese.zip",
            ("mod.json", ModJson),
            (opfer, "UEBERNOMMEN"));

        var r = _installer.Install(zip, _game);

        File.Exists(opfer).Should().BeFalse();
        r.Success.Should().BeFalse("ein Ausbruchsversuch bricht den Install ab");
        r.Message.Should().Contain("herausschreiben");
    }

    [Fact]
    public void Punkt_Punkt_Pfad_wird_abgelehnt()
    {
        var zip = BuildZip("boese2.zip",
            ("mod.json", ModJson),
            ("../../../evil.txt", "boom"));

        var r = _installer.Install(zip, _game);

        r.Success.Should().BeFalse();
        r.Message.Should().Contain("herausschreiben");
    }

    /// <summary>Vorher war der Installer auf ZIP begrenzt, mit der
    /// Begründung, SharpCompress lohne sich für einen reinen
    /// Workshop-Consumer nicht im Bundle. Der Host bringt es mit — die
    /// Begründung ist entfallen, und das steht hier als Zusage.</summary>
    [Fact]
    public void Rar_und_7z_sind_jetzt_mit_dabei()
    {
        _installer.SupportedExtensions.Should().BeEquivalentTo([".zip", ".rar", ".7z"]);
        _installer.HasSupportedExtension("mod.rar").Should().BeTrue();
        _installer.HasSupportedExtension("mod.7z").Should().BeTrue();
        _installer.HasSupportedExtension("liesmich.txt").Should().BeFalse();
    }

    [Fact]
    public void Kein_Archiv_wird_am_Inhalt_erkannt()
    {
        var kaputt = Path.Combine(_tmp, "abgebrochen.zip");
        File.WriteAllText(kaputt, "das ist kein ZIP");

        var r = _installer.Install(kaputt, _game);

        r.Success.Should().BeFalse();
        r.Message.Should().Contain("kein lesbares Archiv");
    }
}
