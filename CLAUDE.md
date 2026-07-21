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

- System-Tray nach Checkmk-Cockpit-Muster (`TrayController`-Service):
  MINIMIEREN legt ins Tray (WindowState-Listener → Hide; Hide schließt nicht,
  daher kein ShutdownMode-Umbau nötig), Schließen-✕ beendet regulär.
  Restore mit `_restoreInProgress`-Guard + `Dispatcher.Post` gegen
  Minimize/Restore-Schleife. App hält die TrayController-Referenz als Feld —
  sonst sammelt der GC das Tray-Icon ein! Fallback ohne Tray = Minimieren normal.
  Eigenes App-Icon (Assets/Klemmbrett.png+.ico, AvaloniaResource +
  ApplicationIcon), ChromeWindow lädt es als Fenster-Icon.
- Auto-Update (`UpdateService`): Check (InformationalVersion vs. Release-Tag,
  proxy-aware, 1×/Start) plus Self-Update: passendes Asset je Plattform
  (win-x64.zip / .AppImage / linux-x64.tar.gz), Download mit Fortschritt,
  Austausch über Helfer-Skript (Windows .bat, Linux .sh) das auf App-Ende
  wartet, Dateien ersetzt und neu startet. AppImage ersetzt sich per $APPIMAGE
  selbst. Ohne passendes Asset → Release-Seite öffnen. Update-Leiste im
  MainWindow (InstallUpdateCommand), PersistOnExit vor dem Neustart.
- Farb-Emoji-Fallback in Program.cs aktiv (🧹-Button) — Inter muss in
  FontManagerOptions erneut gesetzt werden.
- 30-Tage-Persistenz (`HistoryStorageService`): history.json als Index (atomar
  via tmp+move), Bilder als images/<hash>.png (nicht Base64 im JSON!).
  Laden im VM-Ctor, Speichern bei jeder Erfassung + `desktop.Exit`
  (`PersistOnExit`). Retention/Orphan-Cleanup beim Laden und Speichern.
  Defekter Index → .broken-Backup statt Löschen. Tagesfilter als UI-freie
  `HistoryDayFilter`-Logik (testbar); `_history.Entries` = Vollbestand,
  `Entries` = gefilterte Sicht für die Liste. MaxEntries 200.
- Suche: `SearchText` filtert `RebuildEntries` (Text + Bild-Info, case-insensitive),
  kombiniert mit Tagesfilter. Strg+F fokussiert das Suchfeld, Esc → Tray.
- Favoriten: `IsPinned` im Modell; gepinnt = oben (Resort: Pin desc, dann Zeit)
  UND von Retention/MaxEntries ausgenommen; überlebt Storage-Roundtrip.
  TogglePin-Command je Zeile (📌-ToggleButton).
- Schließen-✕ → Tray (OnClosing Cancel + TrayController.MinimizeToTray),
  Fallback ohne Tray = regulär beenden. Kein globaler Hotkey (Avalonia kann
  nur In-App; systemweit bräuchte Win32/X11/Wayland-Extra) — Tray ist der
  Hervorhol-Weg.

## Roadmap

- [Geplante Features in Reihenfolge]

## Referenz

- [Architektur-Entscheidungen, wichtige Klassen, Besonderheiten, bekannte Bugs]
