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

    private void OnEntryDoubleTapped(object? sender, TappedEventArgs e) =>
        (DataContext as MainWindowViewModel)?.CopySelectedCommand.Execute(null);
}
