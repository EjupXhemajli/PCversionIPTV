# EX-IPTV — native Windows-App (C#/WPF + LibVLCSharp)

Neuentwicklung als **echte native Windows-Software**. Der Player ist die
VLC-Engine (LibVLCSharp) als natives Steuerelement – kein Browser, kein
hls.js. Dadurch stabile, hardwarebeschleunigte Wiedergabe von Live-TV,
Filmen und Serien (.ts, .m3u8, .mp4, .mkv).

## Bauen (GitHub Actions – empfohlen)
1. Dieses Verzeichnis in ein GitHub-Repo pushen (Branch `main`).
2. Der Workflow `.github/workflows/build.yml` baut automatisch auf einem
   Windows-Runner eine **einzelne, eigenständige `EX-IPTV.exe`**
   (self-contained, inkl. LibVLC-Native-DLLs – keine .NET-Installation nötig).
3. Unter *Actions → Build EX-IPTV → Artefakt `EX-IPTV-win-x64`* herunterladen.

## Bauen (lokal, falls .NET 8 SDK + Windows vorhanden)
```
dotnet publish EXIPTV/EXIPTV.csproj -c Release -r win-x64 \
  --self-contained true -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

## Projektstruktur
- `EXIPTV.Core/` – plattformunabhängige, **getestete** Logik
  (XtreamClient, M3UParser, Storage, Models).
- `EXIPTV.Core.Tests/` – Unit-Tests (alle grün).
- `EXIPTV/` – WPF-App: Oberfläche, LibVLC-Player, Navigation.

## Umfang dieser ersten nativen Version
Voll: Xtream + M3U laden (mit Retry gegen Panel-Drosselung), Live-TV /
Filme / Serien mit Kategorien, Poster-Grid, Suche, Favoriten, nativer
VLC-Player mit einstellbarem Puffer, Zugangsdaten-Persistenz.

Noch nicht: Serien-Episodenauswahl (Platzhalter), EPG, Aufnahmen.

## v3.0.1 – Startfix
Der Build erzeugt jetzt einen **Ordner** (self-contained) statt einer
Single-File-EXE, weil LibVLC seine nativen DLLs + `plugins`-Ordner an einem
echten Pfad braucht. Artefakte:
- **EX-IPTV-win-x64** (ZIP): entpacken, `EX-IPTV.exe` starten – sofort lauffähig.
- **EX-IPTV-Setup** (Installer, optional): normale Installation mit Startmenü-/Desktop-Verknüpfung.

Falls die App nicht startet, liegt unter `%APPDATA%\EXIPTV\error.log` der Grund.
