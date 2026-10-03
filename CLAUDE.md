# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Grundlagen

- **Was:** Mod-Verwaltung für Captain of Industry (MaFi Games) als KroModIx-Plugin. Steam-AppId **1594320**, Plugin-Id `kroste.captainofindustry`.
- **Stack:** .NET 10, `KroModIx.Plugin.Contracts` als PackageReference, `minHostVersion` 1.27.0.
- **Repo:** `github.com/KroModIx/KroModIx.Plugin.CaptainOfIndustry`.
- **Deploy-Ziel:** `~/.config/KroModIx/plugins/kroste.captainofindustry/` bzw. `%APPDATA%\KroModIx\plugins\kroste.captainofindustry\`.
- **Kroste-Standards:** `~/.claude/skills/KroModIx-Plugin/` — Kernprinzipien, Row-Layout, Release-Ritual. Hier steht nur, was CoI-spezifisch ist.

## Der Sonderfall dieses Plugins: keine Katalog-API

CoI hat **weder Nexus-Präsenz noch nutzbare Workshop-API**. Statt eines Katalog-Tabs gibt es deshalb den **Workshop-Tab mit einer kuratierten Quellenliste**, gepflegt im eigenen Meta-Repo:

- `KroModIx/KroModIx.CoiModIndex` → `sources.json`, gezogen von `raw.githubusercontent.com`, 6 h Cache.
- Jeder Eintrag ist ein GitHub-Repo; optionales `warning`-Feld rendert ein gelbes ⚠-Chip in der Card (Legal-Konflikt, Kompatibilitätsbruch, Adult-Content).
- **Update-Discovery** läuft nicht über eine Mod-API, sondern ordnet installierte Mods über diese Sources-Liste den GitHub-Releases zu.

Daraus folgt: Die meisten Nexus-Muster aus dem Skill (Kernprinzip 8, `NexusFileNameParser`, Enricher) greifen hier **nicht**. Wer sie sucht und nicht findet, sucht richtig — sie fehlen absichtlich.

## Pfade

`CoiPathResolver` probiert mehrere Kandidaten der Reihe nach durch, weil CoI seine Mods je nach Plattform woanders erwartet:

1. `<UserDataDir>/Mods`
2. Proton-Prefix (Linux, Windows-Dokumente im Prefix)
3. `<Dokumente>/Captain of Industry/Mods`
4. `<InstallDir>/Mods` als Fallback

Erster existierender Ordner gewinnt; existiert keiner, wird der Primärkandidat angelegt. Beim Anfassen dieser Kette daran denken, dass Linux-User über Proton **und** über native Dokumente-Pfade kommen können.

## Architektur

- **Services/**: `CoiPathResolver` + `CoiPaths` (Pfade), `CoiModScanner` (`mod.json`-Discovery), `CoiInstallService` / `CoiZipInstaller` (Install, Zip-Slip-Guard), `CoiSourcesService` (Meta-Repo-Liste + Cache), `CoiUpdateChecker` (GitHub-Releases gegen Sources-Liste).
- **Views/**: Workshop, Installiert, Downloads — jeweils View + ViewModel, code-only.
- **`Strings.cs`** liegt im Projekt-Root, nicht unter `Services/` wie bei den Nexus-Plugins.

## Bekannte Grenzen

- **Kein Enable/Disable pro Mod** — was im Mods-Ordner liegt, lädt das Spiel.
- **Update-Check nur für Mods aus der Sources-Liste.** Manuell reinkopierte Mods bleiben ohne Badge, weil es keine Zuordnung zu einem Release gibt.
- Der Update-Check muss off-UI-Thread laufen — er tut es seit v0.4.1, vorher fror die Sidebar beim Prüfen ein.

## Archive und GitHub kommen aus dem Host (ab v0.6.0)

`CoiZipInstaller` bekommt `IHostServices.Archives`, `CoiUpdateChecker` nutzt
`_host.GitHub`. Beide eigenen Wege sind weg.

**Der Installer kann jetzt auch RAR und 7z.** Vorher stand im Code als
Begründung für die ZIP-Beschränkung: „RAR/7z brauchen SharpCompress, das
bringt der reine Workshop-Consumer sonst nicht ins Bundle." Der Host bringt
es mit — die Begründung ist entfallen, und der Downloads-Tab listet nicht
mehr `*.zip`, sondern fragt `_installer.HasSupportedExtension`.

**Der eigene Ausbruch-Schutz prüfte `Contains("..")`.** Das lässt einen
absoluten Eintragsnamen durch, und `Path.Combine` verwirft dann das
Zielverzeichnis. Am Cyberpunk-Installer, der dieselbe Prüfung trug, am
03.10.2026 nachgewiesen: die Datei landete außerhalb des Spiels, und der
Install meldete `Success = true`. Der Mod-Ordner je `mod.json` entsteht
jetzt über `ArchiveExtractOptions.StripPrefix` statt über eigene
Pfad-Arithmetik.

**Bei der Raten-Sperre war der Gewinn hier am größten.** Die Update-Prüfung
läuft in einer Schleife über **alle** installierten Mods. Vorher entdeckte
sie das Limit für jedes Repo neu — eine verbrannte API-Anfrage plus einen
Umleitungs-Aufruf, Mod für Mod. Der Host-Baukasten merkt es sich einmal für
alle Aufrufer. `IsRateLimited` landet als Zusatz im Protokolleintrag, damit
im Zweifel ersichtlich ist, warum Angaben veraltet sein können.

**Beides war ungetestet** — und zwar nicht aus Nachlässigkeit: für den
GitHub-Weg brauchte es eine `IHostServices`-Attrappe, und alle zwölf
Pflichtglieder nachzubauen lohnte sich für einen Test nicht. Deshalb liegt
`FakeHostServices` seit Host v1.33.0 im TestKit. Jetzt 24 Tests, darunter
der Raten-Sperren-Zweig (Tag bekannt, Dateiliste leer — für diese Prüfung
reicht der Tag) und die Fälle, in denen **keine** Abfrage fallen darf:
unbekannter Mod, Mod ohne Versionsangabe, zweiter Aufruf innerhalb der
6h-Frist.

Die Tests fassen das Netz nie an: die Quellen-Liste wird als
`sources-cache.json` in `PluginCacheDir` vorgelegt (6h-Frist), die Ausgaben
kommen aus `FakeGitHubService`.
