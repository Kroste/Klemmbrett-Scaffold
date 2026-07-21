using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using NLog;

namespace Klemmbrett.Services;

/// <summary>
/// Überwacht die Zwischenablage per Polling (Avalonia hat plattformübergreifend
/// kein Change-Event) und meldet neue Texteinträge. Unter Wayland kann das Lesen
/// bei unfokussiertem Fenster fehlschlagen — Fehler werden nur auf Trace geloggt.
/// </summary>
public sealed class ClipboardMonitorService : IDisposable
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(500);

    private readonly ClipboardHistoryService _history;
    private DispatcherTimer? _timer;
    private IClipboard? _clipboard;
    private string? _lastSeen;
    private bool _readInProgress;

    public event Action<string>? EntryCaptured;

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
        Log.Info("Clipboard-Überwachung gestartet (Intervall {Ms} ms)", Interval.TotalMilliseconds);
    }

    /// <summary>Merkt sich selbst gesetzte Inhalte, damit Zurückkopieren keinen Doppel-Log erzeugt.</summary>
    public void NoteOwnWrite(string text) => _lastSeen = text;

    private async void OnTick(object? sender, EventArgs e)
    {
        if (_readInProgress || _clipboard is null)
            return;

        _readInProgress = true;
        try
        {
            var text = await _clipboard.TryGetTextAsync();
            if (string.IsNullOrWhiteSpace(text) || text == _lastSeen)
                return;

            _lastSeen = text;
            _history.Add(text);
            Log.Debug("Neuer Clipboard-Eintrag erfasst ({Length} Zeichen)", text.Length);
            EntryCaptured?.Invoke(text);
        }
        catch (Exception ex)
        {
            // Erwartbar z.B. unter Wayland ohne Fokus oder bei Nicht-Text-Inhalten
            Log.Trace(ex, "Clipboard-Lesen fehlgeschlagen");
        }
        finally
        {
            _readInProgress = false;
        }
    }

    public void Dispose()
    {
        _timer?.Stop();
        _timer = null;
        Log.Info("Clipboard-Überwachung gestoppt");
    }
}
