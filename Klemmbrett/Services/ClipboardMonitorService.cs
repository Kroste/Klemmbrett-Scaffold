using System;
using System.Diagnostics;
using System.IO;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using Klemmbrett.Models;
using NLog;

namespace Klemmbrett.Services;

/// <summary>
/// Überwacht die Zwischenablage per Polling (Avalonia hat plattformübergreifend
/// kein Change-Event) und meldet neue Text- und Bild-Einträge. Bilder werden per
/// SHA-256 über die PNG-Bytes dedupliziert, weil jeder Poll ein neues
/// Bitmap-Objekt liefert. Unter Wayland kann das Lesen bei unfokussiertem
/// Fenster fehlschlagen — Fehler werden nur auf Trace geloggt.
/// </summary>
public sealed class ClipboardMonitorService : IDisposable
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(500);

    private readonly ClipboardHistoryService _history;
    private DispatcherTimer? _timer;
    private IClipboard? _clipboard;
    private string? _lastKey;
    private bool _readInProgress;

    public event Action<IClipboardEntry>? EntryCaptured;

    public ClipboardMonitorService(ClipboardHistoryService history) => _history = history;

    public void Start(TopLevel topLevel)
    {
        _clipboard = topLevel.Clipboard;
        if (_clipboard is null)
        {
            Log.Warn("Kein Clipboard verfügbar — Überwachung nicht gestartet");
            return;
        }

        _timer = new DispatcherTimer(Interval, DispatcherPriority.Background, OnTick);
        _timer.Start();
        Log.Info("Clipboard-Überwachung gestartet (Intervall {Ms} ms, Text + Bilder)",
            Interval.TotalMilliseconds);
    }

    /// <summary>Merkt sich selbst gesetzte Inhalte, damit Zurückkopieren keinen Doppel-Eintrag erzeugt.</summary>
    public void NoteOwnWrite(string dedupeKey) => _lastKey = dedupeKey;

    private async void OnTick(object? sender, EventArgs e)
    {
        if (_readInProgress || _clipboard is null)
            return;

        _readInProgress = true;
        try
        {
            // Text hat Vorrang; Bilder nur pruefen, wenn kein Text vorliegt
            var text = await _clipboard.TryGetTextAsync();
            if (!string.IsNullOrWhiteSpace(text))
            {
                Capture(new TextClipboardEntry(text));
                return;
            }

            var bitmap = await _clipboard.TryGetBitmapAsync();
            if (bitmap is null)
                return;

            var sw = Stopwatch.StartNew();
            using var ms = new MemoryStream();
            bitmap.Save(ms, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
            var hash = ImageClipboardEntry.ComputeHash(ms.ToArray());
            Log.Trace("Clipboard-Bild gehasht in {Ms} ms ({Bytes} Bytes)", sw.ElapsedMilliseconds, ms.Length);

            if ("I:" + hash == _lastKey)
            {
                bitmap.Dispose(); // unveraendertes Bild — Kopie wieder freigeben
                return;
            }

            Capture(new ImageClipboardEntry(bitmap, hash));
        }
        catch (Exception ex)
        {
            // Erwartbar z.B. unter Wayland ohne Fokus oder bei exotischen Formaten
            Log.Trace(ex, "Clipboard-Lesen fehlgeschlagen");
        }
        finally
        {
            _readInProgress = false;
        }
    }

    private void Capture(IClipboardEntry entry)
    {
        if (entry.DedupeKey == _lastKey)
            return;

        _lastKey = entry.DedupeKey;
        _history.Add(entry);
        Log.Debug("Neuer Clipboard-Eintrag erfasst ({Type})", entry.GetType().Name);
        EntryCaptured?.Invoke(entry);
    }

    public void Dispose()
    {
        _timer?.Stop();
        _timer = null;
        Log.Info("Clipboard-Überwachung gestoppt");
    }
}
