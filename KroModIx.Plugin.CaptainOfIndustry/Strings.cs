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
        ["tab.installed"] = "🧩  Installiert",
        ["tab.downloads"] = "📥  Downloads",

        ["btn.refresh"] = "🔄  Neu scannen",
        ["btn.open_steam"] = "Steam öffnen",
        ["btn.open_browser"] = "Browser öffnen",
        ["btn.open_folder"] = "Ordner öffnen",
        ["btn.open_workshop_hub"] = "🌍  Workshop öffnen",
        ["btn.enable"] = "▶  Aktivieren",
        ["btn.disable"] = "⏸  Deaktivieren",
        ["btn.check_updates"] = "🔄  Updates prüfen",
        ["btn.open_release"] = "⬆  Release öffnen",
        ["row.update_badge"] = "⬆ {0} verfügbar",
        ["status.checking_updates"] = "Prüfe Updates …",
        ["status.updates_found"] = "{0} Update(s) verfügbar.",
        ["status.no_updates"] = "Keine Updates gefunden.",
        ["btn.uninstall"] = "🗑  Deinstallieren",
        ["btn.install"] = "📥  Installieren",
        ["btn.install_all"] = "📥  Alle installieren",
        ["btn.delete_file"] = "🗑  Löschen",

        ["placeholder.search"] = "🔍 Filter (Titel/Autor/ID) …",
        ["placeholder.search_mods"] = "🔍 Filter nach Name …",

        ["status.error_prefix"] = "Fehler: ",
        ["workshop.scanning"] = "Suche abonnierte Workshop-Items …",
        ["workshop.no_items"] = "Noch keine abonnierten Workshop-Items für Captain of Industry gefunden.",
        ["workshop.no_items_hint"] = "Steam verwaltet Workshop-Abos — klicke auf „Workshop öffnen“ um dir im Steam-Client oder Browser welche zu abonnieren. Sie erscheinen dann automatisch hier.",
        ["workshop.no_steam_app"] = "Kein Steam-Spiel — Workshop-Discovery nicht möglich.",
        ["workshop.host_too_old"] = "Der Host unterstützt Workshop-Discovery nicht (Contracts < v1.17). App-Update erforderlich.",
        ["workshop.count"] = "{0} abonnierte(s) Workshop-Item(s).",

        ["sources.header"] = "🐙  Kuratierte CoI-Mod-Repos auf GitHub",
        ["sources.hint"] = "Viele CoI-Mods liegen auf GitHub, nicht im Workshop. Klick auf einen Eintrag oeffnet das Repo — Releases dort als .zip laden und im Downloads-Tab installieren.",
        ["sources.count"] = "{0} kuratierte Quelle(n).",
        ["sources.empty_hint"] = "Noch keine Community-Quellen eingetragen — sei der erste!",
        ["sources.btn_open"] = "🐙  Repo öffnen",
        ["sources.btn_contribute"] = "➕  Repo vorschlagen (PR)",

        ["installed.scanning"] = "Scanne Mods-Ordner …",
        ["installed.no_mods"] = "Keine Mods installiert.",
        ["installed.no_mods_hint"] = "Mods liegen unter {0}. Kopiere Mod-Ordner mit mod.json direkt dorthin oder nutze den Downloads-Tab für .zip-Import.",
        ["installed.count"] = "{0} Mod(s) — {1} aktiv, {2} deaktiviert.",
        ["installed.no_dir"] = "Mods-Ordner existiert noch nicht. Wird beim ersten Install angelegt.",

        ["downloads.no_dir"] = "Downloads-Ordner leer: {0}",
        ["downloads.count"] = "{0} Archiv(e) im Downloads-Ordner.",
        ["downloads.installing"] = "Installiere: {0}",
        ["downloads.install_ok"] = "✓ Installiert: {0}",
        ["downloads.install_fail"] = "✗ Install-Fehler: {0}",
        ["downloads.bulk_result"] = "{0} installiert, {1} Fehler.",

        ["row.status_active"] = "aktiv",
        ["row.status_inactive"] = "deaktiviert",

        ["dialog.uninstall_title"] = "Deinstallieren?",
        ["dialog.uninstall_msg"] = "{0} wirklich löschen?\n\nPfad: {1}",
        ["dialog.uninstall_ok"] = "Löschen",
        ["dialog.install_all_title"] = "Alle installieren?",
        ["dialog.install_all_msg"] = "{0} Archiv(e) werden nacheinander in den Mods-Ordner entpackt. Fortfahren?",
        ["dialog.install_all_ok"] = "Installieren",
        ["dialog.delete_zip_title"] = "Archiv löschen?",
        ["dialog.delete_zip_msg"] = "{0} wirklich aus dem Downloads-Ordner löschen? (Bereits installierte Dateien bleiben.)",
        ["dialog.delete_zip_ok"] = "Löschen",
    };

    private static readonly Dictionary<string, string> En = new()
    {
        ["tab.workshop"] = "Workshop",
        ["tab.installed"] = "🧩  Installed",
        ["tab.downloads"] = "📥  Downloads",

        ["btn.refresh"] = "🔄  Rescan",
        ["btn.open_steam"] = "Open in Steam",
        ["btn.open_browser"] = "Open in Browser",
        ["btn.open_folder"] = "Open Folder",
        ["btn.open_workshop_hub"] = "🌍  Open Workshop",
        ["btn.enable"] = "▶  Enable",
        ["btn.disable"] = "⏸  Disable",
        ["btn.check_updates"] = "🔄  Check updates",
        ["btn.open_release"] = "⬆  Open release",
        ["row.update_badge"] = "⬆ {0} available",
        ["status.checking_updates"] = "Checking updates …",
        ["status.updates_found"] = "{0} update(s) available.",
        ["status.no_updates"] = "No updates found.",
        ["btn.uninstall"] = "🗑  Uninstall",
        ["btn.install"] = "📥  Install",
        ["btn.install_all"] = "📥  Install all",
        ["btn.delete_file"] = "🗑  Delete",

        ["placeholder.search"] = "🔍 Filter (title/author/ID) …",
        ["placeholder.search_mods"] = "🔍 Filter by name …",

        ["status.error_prefix"] = "Error: ",
        ["workshop.scanning"] = "Scanning subscribed Workshop items …",
        ["workshop.no_items"] = "No subscribed Workshop items for Captain of Industry yet.",
        ["workshop.no_items_hint"] = "Steam manages Workshop subscriptions — click \"Open Workshop\" to subscribe in the Steam client or browser. They'll show up here automatically.",
        ["workshop.no_steam_app"] = "Not a Steam game — Workshop discovery unavailable.",
        ["workshop.host_too_old"] = "The host doesn't support Workshop discovery (Contracts < v1.17). Please update the app.",
        ["workshop.count"] = "{0} subscribed Workshop item(s).",

        ["sources.header"] = "🐙  Curated CoI mod repos on GitHub",
        ["sources.hint"] = "Many CoI mods live on GitHub, not the Workshop. Click an entry to open the repo — download releases as .zip and install via the Downloads tab.",
        ["sources.count"] = "{0} curated source(s).",
        ["sources.empty_hint"] = "No community sources listed yet — be the first!",
        ["sources.btn_open"] = "🐙  Open repo",
        ["sources.btn_contribute"] = "➕  Suggest repo (PR)",

        ["installed.scanning"] = "Scanning Mods folder …",
        ["installed.no_mods"] = "No mods installed.",
        ["installed.no_mods_hint"] = "Mods live under {0}. Copy mod folders (with mod.json) there directly, or use the Downloads tab for .zip import.",
        ["installed.count"] = "{0} mod(s) — {1} active, {2} disabled.",
        ["installed.no_dir"] = "Mods folder doesn't exist yet. Will be created on first install.",

        ["downloads.no_dir"] = "Downloads folder empty: {0}",
        ["downloads.count"] = "{0} archive(s) in downloads folder.",
        ["downloads.installing"] = "Installing: {0}",
        ["downloads.install_ok"] = "✓ Installed: {0}",
        ["downloads.install_fail"] = "✗ Install error: {0}",
        ["downloads.bulk_result"] = "{0} installed, {1} errors.",

        ["row.status_active"] = "active",
        ["row.status_inactive"] = "disabled",

        ["dialog.uninstall_title"] = "Uninstall?",
        ["dialog.uninstall_msg"] = "Really delete {0}?\n\nPath: {1}",
        ["dialog.uninstall_ok"] = "Delete",
        ["dialog.install_all_title"] = "Install all?",
        ["dialog.install_all_msg"] = "{0} archive(s) will be extracted into the Mods folder one by one. Continue?",
        ["dialog.install_all_ok"] = "Install",
        ["dialog.delete_zip_title"] = "Delete archive?",
        ["dialog.delete_zip_msg"] = "Really remove {0} from the downloads folder? (Already installed files remain.)",
        ["dialog.delete_zip_ok"] = "Delete",
    };
}
