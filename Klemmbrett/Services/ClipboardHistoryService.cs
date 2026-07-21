using System.Collections.Generic;
using System.Linq;
using Klemmbrett.Models;
using NLog;

namespace Klemmbrett.Services;

/// <summary>Verwaltet die Zwischenablage-Historie (neuester Eintrag zuerst, dedupliziert per DedupeKey).</summary>
public sealed class ClipboardHistoryService
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    private readonly List<IClipboardEntry> _entries = [];

    public int MaxEntries { get; init; } = 200;

    public IReadOnlyList<IClipboardEntry> Entries => _entries;

    public void Add(IClipboardEntry? entry)
    {
        if (entry is null)
        {
            Log.Trace("Leerer Clipboard-Eintrag ignoriert");
            return;
        }

        // Duplikat nach vorne ziehen statt doppelt fuehren
        _entries.RemoveAll(e => e.DedupeKey == entry.DedupeKey);
        // Nach dem ersten nicht-gepinnten Eintrag einfügen (gepinnte bleiben oben)
        var insertAt = _entries.FindIndex(e => !e.IsPinned);
        _entries.Insert(insertAt < 0 ? _entries.Count : insertAt, entry);
        // MaxEntries: nur nicht-gepinnte kürzen
        while (_entries.Count(e => !e.IsPinned) > MaxEntries)
        {
            var last = _entries.FindLastIndex(e => !e.IsPinned);
            if (last < 0) break;
            _entries.RemoveAt(last);
        }

        Log.Debug("Clipboard-Eintrag aufgenommen ({Type}, {Count} gesamt)",
            entry.GetType().Name, _entries.Count);
    }

    /// <summary>Sortiert nach Pin-Status (gepinnt zuerst), dann nach Zeit absteigend.</summary>
    public void Resort()
    {
        var sorted = _entries
            .OrderByDescending(e => e.IsPinned)
            .ThenByDescending(e => e.CapturedAt)
            .ToList();
        _entries.Clear();
        _entries.AddRange(sorted);
    }

    public void Clear()
    {
        Log.Info("Clipboard-Historie geleert ({Count} Eintraege)", _entries.Count);
        _entries.Clear();
    }
}
