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
