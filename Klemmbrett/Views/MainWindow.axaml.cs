using Avalonia.Controls;
using Avalonia.Input;
using Klemmbrett.ViewModels;

namespace Klemmbrett.Views;

public partial class MainWindow : ChromeWindow
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => (DataContext as MainWindowViewModel)?.AttachClipboard(this);
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (App.ShouldMinimizeToTray)
        {
            e.Cancel = true;
            Hide(); // Kroste-Tray-Muster: Schliessen minimiert in den Tray
        }
        base.OnClosing(e);
    }

    private void OnEntryDoubleTapped(object? sender, TappedEventArgs e) =>
        (DataContext as MainWindowViewModel)?.CopySelectedCommand.Execute(null);
}
