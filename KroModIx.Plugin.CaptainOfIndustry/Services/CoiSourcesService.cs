using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using KroModIx.Plugin.Contracts;

namespace KroModIx.Plugin.CaptainOfIndustry.Services;

/// <summary>Laedt die kuratierte GitHub-Sources-Liste aus dem Meta-Repo
/// <c>KroModIx/KroModIx.CoiModIndex</c> — analog zum PluginIndex-Muster
/// des Hosts. Damit sieht jede Installation neue Sources sofort nachdem
/// im Meta-Repo ein PR gemerged wurde (ohne Plugin-Rebuild).
///
/// <para>Cache in <c>plugin-cache/sources-cache.json</c>, TTL 6h, Bundle-
/// Fallback wenn Netz-Fehler UND kein Cache. Erste Nutzung im Empty-
/// State des Workshop-Tabs.</para></summary>
public sealed class CoiSourcesService
{
    private const string RawUrl =
        "https://raw.githubusercontent.com/KroModIx/KroModIx.CoiModIndex/main/sources.json";
    private const string ContributionUrl =
        "https://github.com/KroModIx/KroModIx.CoiModIndex";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(6);
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IHostServices _host;
    private readonly string _cachePath;
    private readonly HttpClient _http;

    public CoiSourcesService(IHostServices host)
    {
        _host = host;
        _cachePath = Path.Combine(host.PluginCacheDir, "sources-cache.json");
        _http = host.CreateHttpClient("coi-sources");
    }

    /// <summary>Frontend-URL fuer den „Repo vorschlagen"-Button.</summary>
    public string ContributeUrl => ContributionUrl;

    /// <summary>Fetch mit 6h-TTL. Bei Cache-Miss oder abgelaufen: Netz-
    /// Fetch, bei Erfolg persistieren. Bei Netz-Fehler: alten Cache
    /// zurueckgeben (auch wenn abgelaufen — besser stale als leer).
    /// Am Ende immer eine sinnvolle Antwort — nie null.</summary>
    public async Task<CoiSourcesIndex> GetAsync(bool forceRefresh = false)
    {
        var cachedOk = TryLoadCache(out var cached, out var ageOk);
        if (!forceRefresh && cachedOk && ageOk && cached is not null)
            return cached;
        try
        {
            var text = await _http.GetStringAsync(RawUrl);
            var fresh = JsonSerializer.Deserialize<CoiSourcesIndex>(text, JsonOpts)
                        ?? EmptyIndex();
            try { await File.WriteAllTextAsync(_cachePath, text); }
            catch (Exception ex) { _host.Logger.Debug(ex, "Sources-Cache-Save fehlgeschlagen"); }
            return fresh;
        }
        catch (Exception ex)
        {
            _host.Logger.Debug(ex, "Sources-Fetch fehlgeschlagen — nutze Cache (falls vorhanden)");
            return cached ?? EmptyIndex();
        }
    }

    private bool TryLoadCache(out CoiSourcesIndex? index, out bool ageOk)
    {
        index = null;
        ageOk = false;
        try
        {
            if (!File.Exists(_cachePath)) return false;
            var age = DateTime.UtcNow - File.GetLastWriteTimeUtc(_cachePath);
            ageOk = age < CacheTtl;
            var text = File.ReadAllText(_cachePath);
            index = JsonSerializer.Deserialize<CoiSourcesIndex>(text, JsonOpts);
            return index is not null;
        }
        catch (Exception ex)
        {
            _host.Logger.Debug(ex, "Sources-Cache-Read fehlgeschlagen");
            index = null;
            ageOk = false;
            return false;
        }
    }

    private static CoiSourcesIndex EmptyIndex() => new(1, DateTime.UtcNow, Array.Empty<CoiSourceEntry>());
}

public sealed record CoiSourcesIndex(
    int Schema,
    DateTime UpdatedUtc,
    IReadOnlyList<CoiSourceEntry> Sources);

public sealed record CoiSourceEntry(
    string Repo,
    string DisplayName,
    string Description,
    IReadOnlyList<string>? Tags = null)
{
    public string GitHubUrl => $"https://github.com/{Repo}";
}
