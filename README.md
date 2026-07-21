# Klemmbrett

[![CI](https://github.com/Kroste/Klemmbrett-Scaffold/actions/workflows/ci.yml/badge.svg)](https://github.com/Kroste/Klemmbrett-Scaffold/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/Kroste/Klemmbrett-Scaffold)](https://github.com/Kroste/Klemmbrett-Scaffold/releases)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

Clipboard-Manager mit Verlauf — Desktop-App für Windows und Linux (C# / .NET 10 / Avalonia 12).

<!-- Screenshot: docs/screenshot.png einfügen, sobald die UI steht -->

## Features

- **Automatische Überwachung:** Alles, was mit Strg+C kopiert wird, erscheint
  sofort im Verlauf (neuester Eintrag oben; Duplikate wandern nach vorn)
- **Zurückkopieren:** Doppelklick auf einen Eintrag legt ihn wieder in die
  Zwischenablage
- **Verlauf leeren:** Ein Klick, weg ist alles
- 🔄 **Update-Check:** Prüft GitHub-Releases (proxy-fähig) und meldet neue Versionen

## Installation

Fertige Pakete gibt es auf der [Releases-Seite](https://github.com/Kroste/Klemmbrett-Scaffold/releases):

**Windows:** `Klemmbrett-X.Y.Z-win-x64.zip` herunterladen, entpacken,
`Klemmbrett.exe` starten. Keine Installation nötig (self-contained, .NET ist enthalten).

**Linux (AppImage, empfohlen):** `Klemmbrett-X.Y.Z-x86_64.AppImage` herunterladen,
ausführbar machen und starten:

```bash
chmod +x Klemmbrett-*-x86_64.AppImage
./Klemmbrett-*-x86_64.AppImage
```

**Linux (tar.gz):** `Klemmbrett-X.Y.Z-linux-x64.tar.gz` entpacken und
`./Klemmbrett` starten.

## Bedienung

Klemmbrett starten und einfach normal arbeiten — jeder mit Strg+C kopierte
Text erscheint automatisch im Verlauf (geprüft wird zweimal pro Sekunde).
Doppelklick auf einen Eintrag kopiert ihn zurück in die Zwischenablage,
„Historie leeren" entfernt alle Einträge. Die Statuszeile unten meldet
Aktionen und verfügbare Updates.

Hinweis für Linux/Wayland: Aus Sicherheitsgründen erlaubt Wayland das Lesen
der Zwischenablage teils nur bei fokussiertem Fenster — Einträge erscheinen
dann erst, wenn Klemmbrett wieder den Fokus bekommt.

## Einstellungen

Noch keine — geplant sind maximale Verlaufslänge und Autostart.

## Logs & Fehlersuche

Logdateien liegen im Unterordner `logs/` neben der Anwendung (Tagesarchiv,
14 Tage). Bei einem Problem bitte ein Issue mit der aktuellen Logdatei eröffnen —
Passwörter und Tokens werden automatisch maskiert.

## Entwicklung

```bash
dotnet build   # bauen
dotnet test    # Tests
dotnet run --project Klemmbrett
```

Release: VS-Code-Task „release (tag + push)" — prüft den Git-Zustand, setzt den
Tag und stößt die GitHub-Action an, die alle Pakete baut.

## Lizenz

MIT — siehe [LICENSE](LICENSE).

---

☕ Gefällt dir das Tool? [Buy me a coffee](https://buymeacoffee.com/kroste)
