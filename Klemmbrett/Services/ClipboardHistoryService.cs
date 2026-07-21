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

    public int MaxEntries { get; init; } = 100;

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
        _entries.Insert(0, entry);
        if (_entries.Count > MaxEntries)
            _entries.RemoveRange(MaxEntries, _entries.Count - MaxEntries);

        Log.Debug("Clipboard-Eintrag aufgenommen ({Type}, {Count} gesamt)",
            entry.GetType().Name, _entries.Count);
    }

    public void Clear()
    {
        Log.Info("Clipboard-Historie geleert ({Count} Eintraege)", _entries.Count);
        _entries.Clear();
    }
}
