# KroModIx.Plugin.CaptainOfIndustry

[![CI](https://github.com/KroModIx/KroModIx.Plugin.CaptainOfIndustry/actions/workflows/ci.yml/badge.svg)](https://github.com/KroModIx/KroModIx.Plugin.CaptainOfIndustry/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/KroModIx/KroModIx.Plugin.CaptainOfIndustry)](https://github.com/KroModIx/KroModIx.Plugin.CaptainOfIndustry/releases)

**Captain of Industry Mod-Manager** — Plugin für den
[KroModIx](https://github.com/KroModIx/KroModIx).

Reiner Workshop-Consumer für Captain of Industry (MaFi Games, Steam
AppId 1594320). Listet die vom User abonnierten Steam-Workshop-Items
mit Cover, Titel, Autor, Subscriber-Count. Un/Subscribe bleibt beim
Steam-Client — das Plugin bietet nur Discovery und Deep-Links.

## Features (v0.1.0)

### Workshop-Tab

- **Discovery** via `IHostServices.Workshop` (Contracts v1.17+):
  Filesystem-Scan aller `<SteamLibrary>/steamapps/workshop/content/1594320/`-
  Ordner (Home + externe Platten, Bazzite-Symlink-Split-Dedupe).
- **Enrichment** via Steam-Web-API (`GetPublishedFileDetails`, public,
  kein API-Key nötig): Title, Description, PreviewUrl, Author,
  SubscriberCount, UpdatedUtc.
- **Kroste-Card-Row** mit Cover (140×90 via `_host.Images.DecodeAsync`),
  Titel, Autor · Subscriber · Größe · Datum, Beschreibung (Truncate),
  Actions (Steam öffnen / Browser öffnen / Ordner öffnen).
- **Filter-Textbox** live nach Titel/Autor/Workshop-ID.
- Sortierung: zuletzt lokal aktualisiert zuerst.

### Was das Plugin **nicht** macht

- Kein Un/Subscribe (macht Steam).
- Kein Nexus (CoI ist Workshop-only).
- Kein manueller Install (kein `.zip`-Download).
- Kein IUpdateNotifier (Steam handhabt Updates automatisch).

### Sprachumschaltung

DE + EN. Nach Sprachwechsel im Host: Kachel neu selektieren, dann sind
die frischen Übersetzungen aktiv (Host-Tab-Cache-Invalidate seit v1.14.7).

## Build

```bash
dotnet build -c Release
dotnet test
```

## Lizenz

MIT — siehe [LICENSE](LICENSE).
