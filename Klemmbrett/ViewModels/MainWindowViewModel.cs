using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Klemmbrett.Models;
using Klemmbrett.Services;
using NLog;

namespace Klemmbrett.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private readonly ClipboardHistoryService _history;
    private readonly ClipboardMonitorService _monitor;
    private readonly HistoryStorageService _storage;
    private readonly UpdateService _updateService;
    private IClipboard? _clipboard;

    [ObservableProperty]
    private string _statusText = "Bereit — kopierte Texte und Bilder erscheinen hier";

    [ObservableProperty]
    private IClipboardEntry? _selectedEntry;

    [ObservableProperty]
    private DayOption? _selectedDay;

    [ObservableProperty]
    private string _searchText = string.Empty;

    /// <summary>Gefilterte Sicht auf den Verlauf (abhängig vom Tagesfilter).</summary>
    public ObservableCollection<IClipboardEntry> Entries { get; } = [];

    public ObservableCollection<DayOption> Days { get; } = [];

    public MainWindowViewModel(
        ClipboardHistoryService history,
        ClipboardMonitorService monitor,
        HistoryStorageService storage,
        UpdateService updateService)
    {
        _history = history;
        _monitor = monitor;
        _storage = storage;
        _updateService = updateService;

        // Gespeicherten Verlauf (bis 30 Tage) laden
        foreach (var entry in _storage.Load().AsEnumerable().Reverse())
            _history.Add(entry); // Add dreht die Reihenfolge wieder um (neueste vorn)
        RebuildDays();
        SelectedDay = Days.FirstOrDefault();

        _monitor.EntryCaptured += OnEntryCaptured;
        _ = CheckForUpdateAsync(); // nicht blockierend (Kroste-Standard)
    }

    /// <summary>Vom MainWindow nach dem Laden aufgerufen — startet die Überwachung.</summary>
    public void AttachClipboard(TopLevel topLevel)
    {
        _clipboard = topLevel.Clipboard;
        _monitor.Start(topLevel);
    }

    /// <summary>Beim App-Ende aufgerufen — persistiert den Index.</summary>
    public void PersistOnExit() => _storage.SaveIndex(_history.Entries);

    private void OnEntryCaptured(IClipboardEntry entry)
    {
        if (entry is ImageClipboardEntry image)
            _storage.EnsureImageSaved(image);
        _storage.SaveIndex(_history.Entries); // Index ist klein — sofort sichern

        RebuildDays();
        RebuildEntries();
        StatusText = $"{_history.Entries.Count} Einträge";
    }

    partial void OnSelectedDayChanged(DayOption? value) => RebuildEntries();

    private void RebuildDays()
    {
        var current = SelectedDay;
        var options = HistoryDayFilter.BuildDayOptions(_history.Entries, DateOnly.FromDateTime(DateTime.Now));
        Days.Clear();
        foreach (var o in options)
            Days.Add(o);
        SelectedDay = options.FirstOrDefault(o => o.Date == current?.Date) ?? options.First();
    }

    partial void OnSearchTextChanged(string value) => RebuildEntries();

    private void RebuildEntries()
    {
        var needle = SearchText.Trim();
        Entries.Clear();
        foreach (var entry in _history.Entries
                     .Where(e => HistoryDayFilter.Matches(e, SelectedDay))
                     .Where(e => MatchesSearch(e, needle)))
            Entries.Add(entry);
    }

    private static bool MatchesSearch(IClipboardEntry entry, string needle)
    {
        if (needle.Length == 0) return true;
        return entry switch
        {
            TextClipboardEntry t => t.Text.Contains(needle, StringComparison.OrdinalIgnoreCase),
            ImageClipboardEntry i => i.Info.Contains(needle, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    [RelayCommand]
    private void TogglePin(IClipboardEntry? entry)
    {
        if (entry is null) return;
        entry.IsPinned = !entry.IsPinned;
        Log.Info("Nutzeraktion: Eintrag {State}", entry.IsPinned ? "angeheftet" : "gelöst");
        _history.Resort();
        _storage.SaveIndex(_history.Entries);
        RebuildDays();
        RebuildEntries();
    }

    [RelayCommand]
    private async Task CopySelectedAsync()
    {
        if (SelectedEntry is not { } entry || _clipboard is null)
            return;

        Log.Info("Nutzeraktion: Eintrag zurückkopieren ({Type})", entry.GetType().Name);
        _monitor.NoteOwnWrite(entry.DedupeKey);

        switch (entry)
        {
            case TextClipboardEntry t:
                await _clipboard.SetTextAsync(t.Text);
                break;
            case ImageClipboardEntry i:
                await _clipboard.SetValueAsync(DataFormat.Bitmap, i.Bitmap);
                break;
        }

        StatusText = "In die Zwischenablage kopiert";
    }

    [RelayCommand]
    private void ClearHistory()
    {
        Log.Info("Nutzeraktion: Historie leeren");
        _history.Clear();
        _storage.SaveIndex(_history.Entries); // leert Index + räumt Bilddateien ab
        RebuildDays();
        RebuildEntries();
        StatusText = "Historie geleert";
    }

    private async Task CheckForUpdateAsync()
    {
        var result = await _updateService.CheckForUpdateAsync();
        if (result?.UpdateAvailable == true)
            StatusText = $"Update verfügbar: v{result.Latest}";
    }
}
