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
- Bilder-Support: `IClipboardEntry` (Text/Image) mit `DedupeKey`;
  `TryGetBitmapAsync()` (DataFormat.Bitmap ist universell), Dedupe per SHA-256
  über PNG-Bytes (jeder Poll liefert ein neues Bitmap-Objekt!),
  `Bitmap.Save` braucht `PngBitmapEncoderOptions.Default` (alte Überladung
  obsolet). Zurückkopieren via `SetValueAsync(DataFormat.Bitmap, …)`.
  Bekannte Kosten: Hash-Encoding läuft pro Poll, solange ein Bild anliegt —
  bei Bedarf Optimierung über selteneres Bild-Polling

## Roadmap

- [Geplante Features in Reihenfolge]

## Referenz

- [Architektur-Entscheidungen, wichtige Klassen, Besonderheiten, bekannte Bugs]
