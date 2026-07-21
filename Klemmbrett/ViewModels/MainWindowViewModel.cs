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
    private readonly UpdateService _updateService;
    private IClipboard? _clipboard;

    [ObservableProperty]
    private string _statusText = "Bereit — kopierte Texte und Bilder erscheinen hier";

    [ObservableProperty]
    private IClipboardEntry? _selectedEntry;

    public ObservableCollection<IClipboardEntry> Entries { get; } = [];

    public MainWindowViewModel(
        ClipboardHistoryService history,
        ClipboardMonitorService monitor,
        UpdateService updateService)
    {
        _history = history;
        _monitor = monitor;
        _updateService = updateService;
        _monitor.EntryCaptured += OnEntryCaptured;
        _ = CheckForUpdateAsync(); // nicht blockierend (Kroste-Standard)
    }

    /// <summary>Vom MainWindow nach dem Laden aufgerufen — startet die Überwachung.</summary>
    public void AttachClipboard(TopLevel topLevel)
    {
        _clipboard = topLevel.Clipboard;
        _monitor.Start(topLevel);
    }

    private void OnEntryCaptured(IClipboardEntry entry)
    {
        var existing = Entries.FirstOrDefault(e => e.DedupeKey == entry.DedupeKey);
        if (existing is not null)
            Entries.Remove(existing); // Duplikat nach vorn spiegeln (wie im Service)
        Entries.Insert(0, entry);
        while (Entries.Count > _history.MaxEntries)
            Entries.RemoveAt(Entries.Count - 1);
        StatusText = $"{Entries.Count} Einträge";
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
        Entries.Clear();
        StatusText = "Historie geleert";
    }

    private async Task CheckForUpdateAsync()
    {
        var result = await _updateService.CheckForUpdateAsync();
        if (result?.UpdateAvailable == true)
            StatusText = $"Update verfügbar: v{result.Latest}";
    }
}
