using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using KroModIx.Plugin.Contracts;
using KroModIx.Plugin.CaptainOfIndustry.Views;

namespace KroModIx.Plugin.CaptainOfIndustry;

/// <summary>KroModIx-Plugin fuer Captain of Industry (MaFi Games, AppId 1594320).
/// Reiner Workshop-Consumer — Mods werden ausschliesslich ueber den Steam-
/// Workshop abonniert, das Spiel lade sie automatisch aus
/// <c>&lt;SteamLibrary&gt;/workshop/content/1594320/</c>. Das Plugin listet
/// nur was der User schon abonniert hat (Discovery + Enrichment via
/// <see cref="IHostServices.Workshop"/>) und bietet Deep-Links (Steam-Client,
/// Browser, Ordner). Kein eigener Install/Uninstall, kein Nexus-Katalog,
/// keine BepInEx/MelonLoader-Basis.</summary>
public sealed class CaptainOfIndustryPlugin : IGameModPlugin
{
    public PluginMetadata Metadata { get; } = new(
        Id: "kroste.captainofindustry",
        DisplayName: "Captain of Industry Mod-Manager",
        Version: "0.1.0",
        Author: "Kroste",
        Description: "Mod-Verwaltung fuer Captain of Industry (MaFi Games). " +
            "Workshop-Consumer via _host.Workshop (Contracts v1.17): listet " +
            "die vom User abonnierten Steam-Workshop-Items mit Cover, Titel, " +
            "Autor, Subscriber-Count. Un/Subscribe bleibt beim Steam-Client — " +
            "hier nur Discovery + Deep-Links (Steam-Client, Browser, Ordner). " +
            "DE+EN.");

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

    public Task InitializeAsync(IHostServices host,
        IReadOnlyList<DetectedGame> activatedGames, CancellationToken ct)
    {
        _host = host;
        Strings.Init(host.Localization);
        foreach (var g in activatedGames)
            host.Logger.Info("CoI initialisiert: {Dir}", g.InstallDir);
        return Task.CompletedTask;
    }

    public IEnumerable<IGameTabContribution> GetTabContributions(DetectedGame game)
    {
        if (_host is null) yield break;
        yield return new WorkshopTab(game, _host);
    }

    public Task ShutdownAsync()
    {
        _host?.Logger.Info("CoI shutdown");
        return Task.CompletedTask;
    }

    private sealed class WorkshopTab : IGameTabContribution
    {
        private readonly DetectedGame _game;
        private readonly IHostServices _host;
        public WorkshopTab(DetectedGame game, IHostServices host)
        { _game = game; _host = host; }

        public string Id => "workshop";
        public string Label => Strings.T("tab.workshop");
        public string Icon => "\U0001F30D"; // 🌍
        public int Order => 0;
        public bool IsVisible(DetectedGame game) => true;
        public Control CreateView(DetectedGame game, IHostServices host) =>
            new WorkshopView { DataContext = new WorkshopViewModel(_game, _host) };
    }
}
