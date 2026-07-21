using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Klemmbrett.Services;
using NLog;

namespace Klemmbrett.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private readonly ClipboardHistoryService _history;
    private readonly UpdateService _updateService;

    [ObservableProperty]
    private string _statusText = "Bereit";

    public ObservableCollection<string> Entries { get; } = [];

    public MainWindowViewModel(ClipboardHistoryService history, UpdateService updateService)
    {
        _history = history;
        _updateService = updateService;
        _ = CheckForUpdateAsync(); // nicht blockierend (Kroste-Standard)
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
