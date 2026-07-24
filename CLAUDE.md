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
- AboutWindow (ⓘ-Button in der Bottom-Leiste): Version aus InformationalVersion,
  manueller Update-Check, GitHub/BMC-Links. ChromeWindow + Kroste-Look,
  ShowDialog(owner). WICHTIG: Der automatische Start-Check cacht (max. 1×/Start),
  aber der manuelle Knopf ruft `CheckForUpdateAsync(forceRefresh: true)` — sonst
  meldet er nach einem frisch veröffentlichten Release weiter „aktuell", weil er
  nur den Start-Cache zurückgäbe. Läuft über `MainWindowViewModel.RefreshUpdateAsync`,
  damit auch die Update-Leiste im Hauptfenster erscheint; `null` (Check
  fehlgeschlagen) wird von „aktuell" unterschieden.
- Auto-Update (`UpdateService`): Check (InformationalVersion vs. Release-Tag,
  proxy-aware, 1×/Start) plus Self-Update: passendes Asset je Plattform
  (win-x64.zip / .AppImage / linux-x64.tar.gz), Download mit Fortschritt,
  Austausch über Helfer-Skript, das auf App-Ende wartet, ersetzt, neu startet.
  WICHTIG Windows: Batch-Zeilen OHNE Einrückung (eingerücktes :label bricht goto);
  Warten via powershell Wait-Process statt tasklist-Schleife; update.log im
  Work-Ordner. Linux AppImage: cp -f statt mv (Loop-Device gemountet → "busy"),
  Neustart via setsid. Bekannte Baustelle: end-to-end nur mit echten Releases
  testbar. Ohne passendes Asset → Release-Seite öffnen. Update-Leiste im
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
- Löschen einzelner Einträge: `ClipboardHistoryService.Remove` (per DedupeKey),
  `DeleteEntryCommand` (🗑 je Zeile + Entf-Taste, Fallback auf SelectedEntry).
  Bild-Einträge: SaveIndex verschiebt die verwaiste PNG in den Trash (kein Sofortlöschen —
  siehe „Trend-Micro-Robustheit").
- Passwort-Maskierung: `SecretDetector.LooksLikeSecret` (testbare, bewusst
  konservative Heuristik) erkennt beschriftete Secrets (`password=…`, `token:…`
  via Regex) UND alleinstehende passwortartige Tokens (kein Leerraum, Länge 8–256,
  keine URL/E-Mail, ≥3 Zeichenklassen INKL. Ziffer — die Ziffer-Pflicht hält
  CamelCase-Namen/Hex/Base64 draußen). `TextClipboardEntry.IsSecret` einmalig im
  Ctor; `DisplayPreview` zeigt `••••••••` bis `IsRevealed` (👁-ToggleButton,
  Nur-Sitzung, NICHT persistiert → Secrets starten nach Neustart maskiert).
  Volltext bleibt fürs Zurückkopieren erhalten (Maskierung = Anzeige).
- Inline-Secret-Verschlüsselung (`ISecretProtector` / `SecretProtector`): als Secret
  erkannte Text-Einträge werden **einzeln** in `history.json` verschlüsselt (Feld
  `TextEnc`, Präfix `ENC1:<base64>`); der Rest bleibt lesbarer Klartext-JSON. Bewusst
  KEIN opaker Gesamt-Blob wie in WebExStudios `CredentialVault` — Verhaltens-AV
  (Trend Micro Behavior Monitoring) reagiert auf entropiereiche Klumpen und würde
  Fehlalarm werfen. Schlüssel: **DPAPI CurrentUser** unter Windows, **AES-256-GCM
  mit lokalem Master-Key** (`%APPDATA%/Klemmbrett/protect.key`, 0600) unter Linux/macOS.
  Legacy-Klartext-Einträge werden weiter gelesen und beim nächsten `SaveIndex`
  transparent nach `TextEnc` migriert. Fehlende/kaputte Chiffrate (User-Wechsel,
  DPAPI-Profil weg) → Eintrag wird verworfen, KEIN Crash. TESTS nutzen `TestProtector`
  (Base64-Fake ohne echte Krypto), damit sie plattformunabhängig laufen.
- Kommentare je Eintrag: `Comment` im Interface/Modell, persistiert (StoredEntry
  bekam optionales `Comment`-Feld → alte history.json lädt kompatibel weiter).
  Entry-Modelle sind jetzt `ObservableObject` (Basisklasse `ClipboardEntry`),
  damit Comment/IsRevealed/IsEditingComment live binden. 💬-ToggleButton klappt
  einen Inline-Editor (`IsEditingComment`) aus; Persistenz über `LostFocus` →
  `MainWindowViewModel.NotifyCommentChanged` (SaveIndex + RebuildEntries).
  Kommentar erscheint als kursive Gold-Zeile unter dem Eintrag (`HasComment`).
  Suche (`MatchesSearch`) prüft zusätzlich `Comment`. WICHTIG: Entf-Taste im
  MainWindow löscht nur, wenn KEIN TextBox fokussiert ist (sonst löscht das
  Tippen im Kommentar-/Suchfeld den Eintrag) — Guard via `FocusManager`.
- Schließen-✕ → Tray (OnClosing Cancel + TrayController.MinimizeToTray),
  Fallback ohne Tray = regulär beenden. Kein globaler Hotkey (Avalonia kann
  nur In-App; systemweit bräuchte Win32/X11/Wayland-Extra) — Tray ist der
  Hervorhol-Weg.
- Single-Instance (`SingleInstanceGuard`): zweite Starts sind verboten — sonst
  kämpfen zwei Prozesse um Clipboard-Poll, `history.json` (Save-Race) und Tray-Icon.
  Umsetzung via Named-Pipe (`Klemmbrett.SingleInstance.<User>`, .NET nutzt auf
  Linux/macOS Unix-Sockets unter `/tmp/CoreFxPipe_<name>`). Ablauf: `Program.Main`
  ruft `TryClaim()` **vor** `StartWithClassicDesktopLifetime` — schlägt es fehl,
  wird ein `ACTIVATE`-Byte an die primäre Instance gesendet (`NotifyPrimary`) und
  der Prozess beendet sich mit `return 0`, ohne dass Avalonia bootet. Die primäre
  Instance bekommt das Signal per ThreadPool-Listener, postet auf den UI-Dispatcher
  und ruft `TrayController.Restore()` (Fallback: MainWindow.Show/Activate). Verwaiste
  Sockets nach Crash werden über einen Connect-Test erkannt und beim Retry gelöscht.
  Der Guard wird beim `desktop.Exit` disposed, damit die Pipe frei wird.
  User-spezifischer Pipe-Name → mehrere Benutzer auf einer Kiste blockieren einander
  nicht (Terminalserver-Fall).
- Trend-Micro-Robustheit / „Historie leeren": früher hat
  `CleanupOrphanImages` in einer engen Schleife alle PNGs gelöscht — Trend Micros
  Behavior Monitoring hat das als Wiper/Ransomware klassifiziert und **die App**
  selbst gelöscht. Fix: verwaiste PNGs werden nach `images.trash/<ts>-<hash>.png`
  **verschoben** (File.Move, kein Delete). `TrashCleanupService` (Singleton, im
  DI-Container registriert, fire-and-forget via `_ = Task.Run(…)` beim App-Start)
  räumt nur Dateien älter `MinAge` (default 10 Min) mit `Throttle` (default 150 ms)
  Pause zwischen Löschungen weg → hält den Delete-Fluss unter AV-Verdachtsschwellen.
  WICHTIG: das ist ein Verhaltens-Fix, keine Signatur-Immunität — App bleibt
  unsigniert, im Zweifel Ausnahme bei Trend Micro eintragen lassen.

## Roadmap

- [Geplante Features in Reihenfolge]

## Referenz

- [Architektur-Entscheidungen, wichtige Klassen, Besonderheiten, bekannte Bugs]
