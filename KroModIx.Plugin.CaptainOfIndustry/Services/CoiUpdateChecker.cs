using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
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
/// <para>Rate-Limit: die GitHub-API erlaubt unauthentifiziert 60 Requests/h
/// pro IP. Deshalb 6h-TTL auf dem Ergebnis und bei HTTP 403 der
/// Redirect-Chase auf <c>/releases/latest</c> (kein API-Call, kein Limit) —
/// dasselbe Muster wie im Host-PluginUpdateService.</para></summary>
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
        try { installed = _scanner.Scan(game).ToList(); }
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

        using var http = new HttpClient(_host.CreateHttpClientHandler())
        {
            Timeout = TimeSpan.FromSeconds(20),
        };
        // GitHub verlangt einen User-Agent, sonst 403 auf jeden API-Call.
        http.DefaultRequestHeaders.UserAgent.ParseAdd("KroModIx-CoI-UpdateCheck");

        var found = new List<CoiUpdateCandidate>();
        foreach (var mod in installed)
        {
            ct.ThrowIfCancellationRequested();
            var source = MatchSource(mod, index.Sources);
            if (source is null) continue;
            if (string.IsNullOrWhiteSpace(mod.Version)) continue;

            var tag = await TryGetLatestTagAsync(http, source.Repo, ct).ConfigureAwait(false);
            if (tag is null) continue;
            if (!VersionCompare.IsNewer(tag, mod.Version)) continue;

            found.Add(new CoiUpdateCandidate(
                InstalledName: mod.DisplayName,
                FolderName: mod.FolderName,
                InstalledVersion: mod.Version!,
                LatestVersion: tag,
                Repo: source.Repo,
                ReleaseUrl: $"https://github.com/{source.Repo}/releases/latest"));
        }

        _pending = found;
        _lastCheckUtc = DateTime.UtcNow;
        Log.Info("CoI-Update-Check: {N} Update(s) fuer {Installed} Mod(s)", found.Count, installed.Count);
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

    private async Task<string?> TryGetLatestTagAsync(HttpClient http, string repo, CancellationToken ct)
    {
        try
        {
            using var resp = await http.GetAsync(
                $"https://api.github.com/repos/{repo}/releases/latest", ct).ConfigureAwait(false);
            if (resp.StatusCode == HttpStatusCode.Forbidden || resp.StatusCode == HttpStatusCode.TooManyRequests)
            {
                Log.Debug("GitHub-API Rate-Limit fuer {Repo} — Redirect-Chase", repo);
                return await TryRedirectChaseAsync(repo, ct).ConfigureAwait(false);
            }
            if (!resp.IsSuccessStatusCode)
            {
                Log.Debug("Kein Release fuer {Repo}: HTTP {Code}", repo, (int)resp.StatusCode);
                return null;
            }
            var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("tag_name", out var tag)
                ? tag.GetString()
                : null;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Release-Abfrage fehlgeschlagen: {Repo}", repo);
            return null;
        }
    }

    /// <summary>Ohne API: /releases/latest antwortet mit 302 auf
    /// /releases/tag/&lt;tag&gt;. Kostet kein Rate-Limit-Budget.</summary>
    private async Task<string?> TryRedirectChaseAsync(string repo, CancellationToken ct)
    {
        try
        {
            var handler = _host.CreateHttpClientHandler();
            handler.AllowAutoRedirect = false;
            using var noRedirect = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
            noRedirect.DefaultRequestHeaders.UserAgent.ParseAdd("KroModIx-CoI-UpdateCheck");
            using var resp = await noRedirect.GetAsync(
                $"https://github.com/{repo}/releases/latest", ct).ConfigureAwait(false);
            var location = resp.Headers.Location?.ToString();
            if (string.IsNullOrEmpty(location)) return null;
            var idx = location.IndexOf("/tag/", StringComparison.Ordinal);
            return idx < 0 ? null : location[(idx + 5)..];
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Redirect-Chase fehlgeschlagen: {Repo}", repo);
            return null;
        }
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
