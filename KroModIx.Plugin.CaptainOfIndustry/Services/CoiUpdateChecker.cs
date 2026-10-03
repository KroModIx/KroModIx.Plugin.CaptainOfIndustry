using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KroModIx.Plugin.Contracts;
using NLog;

namespace KroModIx.Plugin.CaptainOfIndustry.Services;

/// <summary>Update-Discovery fuer installierte CoI-Mods (v0.4.0).
///
/// <para><b>Datenquelle:</b> die kuratierte Sources-Liste
/// (<see cref="CoiSourcesService"/>) plus die GitHub-Releases des jeweiligen
/// Repos. Ein installierter Mod wird ueber seinen Namen einem Source-Eintrag
/// zugeordnet; dessen neuestes Release-Tag wird gegen die Version aus der
/// <c>mod.json</c> verglichen.</para>
///
/// <para><b>Warum kein Manifest-Store wie bei den Nexus-Plugins:</b> CoI-Mods
/// landen ueber den Downloads-Tab als lokale ZIPs im Mods-Ordner — es gibt
/// keinen Download-Weg im Plugin, der eine Herkunft mitschreiben koennte.
/// Das Namens-Matching ist die ehrliche Naeherung; was nicht matcht, meldet
/// einfach kein Update statt zu raten.</para>
///
/// <para><b>Workshop-Mods sind bewusst aussen vor</b> — die aktualisiert
/// Steam selbst, ein zweiter Update-Kanal waere nur verwirrend.</para>
///
/// <para><b>Rate-Limit: seit v0.6.0 über <c>IHostServices.GitHub</c></b>
/// (Host v1.32.0). Die GitHub-API erlaubt unauthentifiziert 60 Anfragen pro
/// Stunde; der Host-Baukasten merkt sich die Sperre <b>einmal für alle</b>
/// Aufrufer und weicht dann auf den Umleitungs-Pfad aus
/// (<c>/releases/latest</c> ohne Folgen der Umleitung, kein API-Aufruf).
/// Das ist hier mehr als eine gesparte Zeile: diese Prüfung läuft in einer
/// Schleife über <b>alle</b> installierten Mods. Vorher entdeckte sie das
/// Limit für jedes Repo neu — eine verbrannte Anfrage plus einen
/// Umleitungs-Aufruf, Mod für Mod. Die 6h-TTL auf dem Ergebnis bleibt.</para>
///
/// <para>Ein <c>GITHUB_TOKEN</c> in der Umgebung hebt das Limit auf 5000
/// Anfragen pro Stunde; der Host nimmt es automatisch mit.</para></summary>
public sealed class CoiUpdateChecker
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    private static readonly TimeSpan CheckTtl = TimeSpan.FromHours(6);

    private readonly CoiModScanner _scanner;
    private readonly CoiSourcesService _sources;
    private readonly IHostServices _host;

    private IReadOnlyList<CoiUpdateCandidate> _pending = Array.Empty<CoiUpdateCandidate>();
    private DateTime _lastCheckUtc;

    public CoiUpdateChecker(CoiModScanner scanner, CoiSourcesService sources, IHostServices host)
    {
        _scanner = scanner;
        _sources = sources;
        _host = host;
    }

    public IReadOnlyList<CoiUpdateCandidate> Pending => _pending;
    public int PendingCount => _pending.Count;
    public DateTime LastCheckUtc => _lastCheckUtc;

    /// <summary>Prueft alle installierten Mods gegen die Sources-Liste.
    /// Rueckgabe: Anzahl gefundener Updates. <paramref name="force"/>
    /// umgeht die 6h-TTL (Button „Updates pruefen").</summary>
    public async Task<int> CheckAsync(DetectedGame game, bool force = false,
        CancellationToken ct = default)
    {
        if (!force && _lastCheckUtc != default && DateTime.UtcNow - _lastCheckUtc < CheckTtl)
            return _pending.Count;

        List<CoiMod> installed;
        try
        {
            // v0.4.1: Task.Run ist Pflicht, nicht Deko. CheckAsync wird aus dem
            // CheckUpdatesCommand aufgerufen, also vom UI-Thread — und async
            // schuetzt davor nicht: bis zum ersten await laeuft alles auf dem
            // Caller-Thread. Der Mod-Scan liest das Mods-Verzeichnis samt
            // mod.json pro Ordner und fror damit die Sidebar ein
            // (Kernprinzip 3).
            installed = await Task.Run(() => _scanner.Scan(game).ToList(), ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Warn(ex, "Update-Check: Mod-Scan fehlgeschlagen");
            return _pending.Count;
        }
        if (installed.Count == 0)
        {
            _pending = Array.Empty<CoiUpdateCandidate>();
            _lastCheckUtc = DateTime.UtcNow;
            return 0;
        }

        var index = await _sources.GetAsync().ConfigureAwait(false);
        if (index.Sources.Count == 0) return _pending.Count;

        var found = new List<CoiUpdateCandidate>();
        foreach (var mod in installed)
        {
            ct.ThrowIfCancellationRequested();
            var source = MatchSource(mod, index.Sources);
            if (source is null) continue;
            if (string.IsNullOrWhiteSpace(mod.Version)) continue;

            var release = await _host.GitHub.GetLatestReleaseAsync(source.Repo, ct)
                .ConfigureAwait(false);
            if (release is null) continue;
            if (!VersionCompare.IsNewer(release.Tag, mod.Version)) continue;

            found.Add(new CoiUpdateCandidate(
                InstalledName: mod.DisplayName,
                FolderName: mod.FolderName,
                InstalledVersion: mod.Version!,
                LatestVersion: release.Tag,
                Repo: source.Repo,
                ReleaseUrl: release.HtmlUrl
                            ?? $"https://github.com/{source.Repo}/releases/latest"));
        }

        _pending = found;
        _lastCheckUtc = DateTime.UtcNow;
        Log.Info("CoI-Update-Check: {N} Update(s) fuer {Installed} Mod(s){Limit}",
            found.Count, installed.Count,
            _host.GitHub.IsRateLimited ? " (GitHub-Limit erreicht, Angaben koennen veralten)" : "");
        return found.Count;
    }

    /// <summary>Ordnet einen installierten Mod einem Source-Eintrag zu.
    /// Verglichen wird normalisiert (nur Buchstaben/Ziffern, lowercase)
    /// gegen DisplayName, Ordnername und den Repo-Basisnamen — CoI-Mods
    /// heissen im Ordner selten exakt wie im Repo.</summary>
    public static CoiSourceEntry? MatchSource(CoiMod mod, IReadOnlyList<CoiSourceEntry> sources)
    {
        var candidates = new[] { mod.DisplayName, mod.FolderName }
            .Select(NormalizeName)
            .Where(n => n.Length >= 3)
            .ToList();
        if (candidates.Count == 0) return null;

        foreach (var source in sources)
        {
            var repoBase = source.Repo.Contains('/')
                ? source.Repo[(source.Repo.IndexOf('/') + 1)..]
                : source.Repo;
            var names = new[] { NormalizeName(source.DisplayName), NormalizeName(repoBase) }
                .Where(n => n.Length >= 3)
                .ToList();
            foreach (var name in names)
                foreach (var candidate in candidates)
                    if (candidate.Equals(name, StringComparison.Ordinal)
                        || candidate.Contains(name, StringComparison.Ordinal)
                        || name.Contains(candidate, StringComparison.Ordinal))
                        return source;
        }
        return null;
    }

    private static string NormalizeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        Span<char> buffer = stackalloc char[value.Length];
        int n = 0;
        foreach (var c in value)
            if (char.IsLetterOrDigit(c)) buffer[n++] = char.ToLowerInvariant(c);
        return new string(buffer[..n]);
    }

}

/// <summary>Ein gefundenes Update. <see cref="ReleaseUrl"/> zeigt auf die
/// GitHub-Release-Seite — installiert wird wie gehabt ueber den
/// Downloads-Tab, das Plugin laedt nichts eigenmaechtig herunter.</summary>
public sealed record CoiUpdateCandidate(
    string InstalledName,
    string FolderName,
    string InstalledVersion,
    string LatestVersion,
    string Repo,
    string ReleaseUrl);
