using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using KroModIx.Plugin.Contracts;
using KroModIx.Plugin.CaptainOfIndustry.Services;
using KroModIx.Plugin.CaptainOfIndustry.Views;

namespace KroModIx.Plugin.CaptainOfIndustry;

/// <summary>KroModIx-Plugin fuer Captain of Industry (MaFi Games, AppId 1594320).
/// Drei Tabs: Workshop (Steam-Workshop-Consumer via _host.Workshop),
/// Installiert (manuelle Mods im Docs-Mods-Ordner mit mod.json), Downloads
/// (Local .zip-Import in den Mods-Ordner).</summary>
public sealed class CaptainOfIndustryPlugin : IGameModPlugin
{
    public PluginMetadata Metadata { get; } = new(
        Id: "kroste.captainofindustry",
        DisplayName: "Captain of Industry Mod-Manager",
        Version: "0.3.1",
        Author: "Kroste",
        Description: "Mod-Verwaltung fuer Captain of Industry (MaFi Games). " +
            "v0.3.1: Sources-Cards rendern jetzt ein gelbes ⚠-Warning-Chip " +
            "wenn im Meta-Repo ein optionales warning-Feld gesetzt ist (z.B. " +
            "Legal-Konflikt, Kompatibilitaets-Bruch, Adult-Content). " +
            "v0.3.0: Workshop-Empty-State mit kuratierter GitHub-Sources-Liste " +
            "aus KroModIx/KroModIx.CoiModIndex (6h-Cache). v0.2.0: Drei Tabs " +
            "— Workshop, Installiert (mod.json-Discovery), Downloads (Local " +
            ".zip-Import). v0.1.0: Workshop-Consumer. DE+EN.");

    public IReadOnlyList<GameTarget> Targets { get; } = new[]
    {
        new GameTarget(
            GameId: "captain-of-industry",
            DisplayName: "Captain of Industry",
            SteamAppId: 1594320,
            AlternativeExecutableNames: new[] { "CaptainOfIndustry.exe", "CaptainOfIndustry" },
            Platforms: Platforms.Both),
    };

    private IHostServices? _host;
    private CoiPathResolver? _paths;
    private CoiModScanner? _scanner;
    private CoiInstallService? _installer;
    private CoiZipInstaller? _zipInstaller;
    private CoiPaths? _pluginPaths;
    private DownloadEventBus? _bus;

    public Task InitializeAsync(IHostServices host,
        IReadOnlyList<DetectedGame> activatedGames, CancellationToken ct)
    {
        _host = host;
        Strings.Init(host.Localization);
        _paths = new CoiPathResolver();
        _scanner = new CoiModScanner(_paths);
        _installer = new CoiInstallService();
        _zipInstaller = new CoiZipInstaller(_paths);
        _pluginPaths = new CoiPaths(host);
        _bus = new DownloadEventBus();
        foreach (var g in activatedGames)
            host.Logger.Info("CoI initialisiert: {Dir}", g.InstallDir);
        return Task.CompletedTask;
    }

    public IEnumerable<IGameTabContribution> GetTabContributions(DetectedGame game)
    {
        if (_host is null || _paths is null || _scanner is null || _installer is null
            || _zipInstaller is null || _pluginPaths is null || _bus is null)
            yield break;
        yield return new WorkshopTab(game, _host);
        yield return new InstalledTab(game, _scanner, _installer, _paths, _bus, _host);
        yield return new DownloadsTab(game, _pluginPaths, _zipInstaller, _bus, _host);
    }

    public Task ShutdownAsync()
    {
        _host?.Logger.Info("CoI shutdown");
        return Task.CompletedTask;
    }

    private sealed class WorkshopTab : IGameTabContribution
    {
        private readonly DetectedGame _game; private readonly IHostServices _host;
        public WorkshopTab(DetectedGame g, IHostServices h) { _game = g; _host = h; }
        public string Id => "workshop";
        public string Label => Strings.T("tab.workshop");
        public string Icon => "\U0001F30D"; // 🌍
        public int Order => 0;
        public bool IsVisible(DetectedGame game) => true;
        public Control CreateView(DetectedGame game, IHostServices host) =>
            new WorkshopView { DataContext = new WorkshopViewModel(_game, _host) };
    }

    private sealed class InstalledTab : IGameTabContribution
    {
        private readonly DetectedGame _game;
        private readonly CoiModScanner _scanner;
        private readonly CoiInstallService _installer;
        private readonly CoiPathResolver _paths;
        private readonly DownloadEventBus _bus;
        private readonly IHostServices _host;
        public InstalledTab(DetectedGame g, CoiModScanner s, CoiInstallService i,
            CoiPathResolver p, DownloadEventBus b, IHostServices h)
        { _game = g; _scanner = s; _installer = i; _paths = p; _bus = b; _host = h; }
        public string Id => "installed";
        public string Label => Strings.T("tab.installed");
        public string Icon => "\U0001F9E9"; // 🧩
        public int Order => 10;
        public bool IsVisible(DetectedGame game) => true;
        public Control CreateView(DetectedGame game, IHostServices host) =>
            new InstalledModsView
            {
                DataContext = new InstalledModsViewModel(_game, _scanner, _installer, _paths, _bus, _host),
            };
    }

    private sealed class DownloadsTab : IGameTabContribution
    {
        private readonly DetectedGame _game;
        private readonly CoiPaths _paths;
        private readonly CoiZipInstaller _installer;
        private readonly DownloadEventBus _bus;
        private readonly IHostServices _host;
        public DownloadsTab(DetectedGame g, CoiPaths p, CoiZipInstaller i,
            DownloadEventBus b, IHostServices h)
        { _game = g; _paths = p; _installer = i; _bus = b; _host = h; }
        public string Id => "downloads";
        public string Label => Strings.T("tab.downloads");
        public string Icon => "\U0001F4E5"; // 📥
        public int Order => 20;
        public bool IsVisible(DetectedGame game) => true;
        public Control CreateView(DetectedGame game, IHostServices host) =>
            new DownloadsView
            {
                DataContext = new DownloadsViewModel(_game, _paths, _installer, _bus, _host),
            };
    }
}
