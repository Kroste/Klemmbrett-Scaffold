using System.Collections.Generic;
using System.Linq;
using NLog;

namespace Klemmbrett.Services;

/// <summary>Verwaltet die Zwischenablage-Historie (neuester Eintrag zuerst, dedupliziert).</summary>
public sealed class ClipboardHistoryService
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    private readonly List<string> _entries = [];

    public int MaxEntries { get; init; } = 100;

    public IReadOnlyList<string> Entries => _entries;

    public void Add(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            Log.Trace("Leerer Clipboard-Inhalt ignoriert");
            return;
        }

        _entries.Remove(text); // Duplikat nach vorne ziehen statt doppelt führen
        _entries.Insert(0, text);
        if (_entries.Count > MaxEntries)
            _entries.RemoveRange(MaxEntries, _entries.Count - MaxEntries);

        Log.Debug("Clipboard-Eintrag aufgenommen ({Length} Zeichen, {Count} gesamt)",
            text.Length, _entries.Count);
    }

    public void Clear()
    {
        Log.Info("Clipboard-Historie geleert ({Count} Einträge)", _entries.Count);
        _entries.Clear();
    }
}
