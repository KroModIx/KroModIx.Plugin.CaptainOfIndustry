using System.Collections.Generic;
using KroModIx.Plugin.Contracts;

namespace KroModIx.Plugin.CaptainOfIndustry;

/// <summary>Lokalisierte UI-Strings. Language-Umschaltung geht ueber
/// <see cref="ILocalization.Culture"/> — der Host sendet CultureChanged
/// beim Wechsel, das Plugin re-liest den Language-Cache bei jedem
/// <see cref="T"/>-Aufruf.</summary>
internal static class Strings
{
    private static ILocalization? _loc;
    public static void Init(ILocalization loc) => _loc = loc;

    public static string T(string key)
    {
        var isEn = string.Equals(_loc?.CurrentIso, "en", System.StringComparison.OrdinalIgnoreCase);
        var dict = isEn ? En : De;
        return dict.TryGetValue(key, out var v) ? v : key;
    }

    private static readonly Dictionary<string, string> De = new()
    {
        ["tab.workshop"] = "Workshop",

        ["btn.refresh"] = "🔄  Neu scannen",
        ["btn.open_steam"] = "Steam öffnen",
        ["btn.open_browser"] = "Browser öffnen",
        ["btn.open_folder"] = "Ordner öffnen",

        ["placeholder.search"] = "🔍 Filter (Titel/Autor/ID) …",

        ["status.error_prefix"] = "Fehler: ",
        ["workshop.scanning"] = "Suche abonnierte Workshop-Items …",
        ["workshop.no_items"] = "Keine abonnierten Workshop-Items für Captain of Industry gefunden. Abonniere Mods im Steam-Workshop, sie erscheinen dann hier.",
        ["workshop.no_steam_app"] = "Kein Steam-Spiel — Workshop-Discovery nicht möglich.",
        ["workshop.host_too_old"] = "Der Host unterstützt Workshop-Discovery nicht (Contracts < v1.17). App-Update erforderlich.",
        ["workshop.count"] = "{0} abonnierte(s) Workshop-Item(s).",
    };

    private static readonly Dictionary<string, string> En = new()
    {
        ["tab.workshop"] = "Workshop",

        ["btn.refresh"] = "🔄  Rescan",
        ["btn.open_steam"] = "Open in Steam",
        ["btn.open_browser"] = "Open in Browser",
        ["btn.open_folder"] = "Open Folder",

        ["placeholder.search"] = "🔍 Filter (title/author/ID) …",

        ["status.error_prefix"] = "Error: ",
        ["workshop.scanning"] = "Scanning subscribed Workshop items …",
        ["workshop.no_items"] = "No subscribed Workshop items for Captain of Industry. Subscribe to mods in the Steam Workshop — they'll appear here.",
        ["workshop.no_steam_app"] = "Not a Steam game — Workshop discovery unavailable.",
        ["workshop.host_too_old"] = "The host doesn't support Workshop discovery (Contracts < v1.17). Please update the app.",
        ["workshop.count"] = "{0} subscribed Workshop item(s).",
    };
}
