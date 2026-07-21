# Klemmbrett

## Grundlagen

- **Was:** [Ein Satz: Was macht die App?]
- **Stack:** C# / .NET 10 / Avalonia 12, CommunityToolkit.Mvvm, Microsoft.Extensions.DependencyInjection, NLog (mit Secret-Masking), xUnit + FluentAssertions 7.x
- **Struktur:** Flach (kein `src/`), `.slnx`, Central Package Management, `Directory.Build.props`, MinVer (Tags `v*`)
- **Konventionen:** GlobalExceptionHandler, InfoWindow mit Version + BMC-Button, `TreatWarningsAsErrors`
- **Kommunikation:** Deutsch, "du". Lars entwirft, Claude implementiert.

## Aktueller Stand

- Grundgerüst nach Kroste-Standards, Build grün, 19 Tests
- Clipboard-Überwachung: `ClipboardMonitorService` pollt per DispatcherTimer
  (500 ms) — Avalonia hat kein Clipboard-Change-Event. Lesen via
  `TryGetTextAsync()`-Extension (Avalonia 12: `GetTextAsync` existiert nicht mehr)
- Doppelklick auf Eintrag = Zurückkopieren (`NoteOwnWrite` verhindert
  Selbst-Erfassung); Wayland liest teils nur bei Fokus

## Roadmap

- [Geplante Features in Reihenfolge]

## Referenz

- [Architektur-Entscheidungen, wichtige Klassen, Besonderheiten, bekannte Bugs]
