using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Klemmbrett.Services;
using Klemmbrett.ViewModels;
using Microsoft.Extensions.DependencyInjection;

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
        // Schließen-✕ minimiert ins Tray, wenn verfügbar (App läuft weiter);
        // wirklich beendet wird über das Tray-Menü „Beenden".
        if (App.Tray is { IsAvailable: true } tray)
        {
            e.Cancel = true;
            tray.MinimizeToTray();
        }
        base.OnClosing(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // In-App-Hotkeys: Strg+F → Suche fokussieren, Esc → ins Tray
        if (e.Key == Key.F && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            this.FindControl<TextBox>("SearchBox")?.Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && App.Tray is { IsAvailable: true } tray)
        {
            tray.MinimizeToTray();
            e.Handled = true;
        }
        else if (e.Key == Key.Delete && DataContext is MainWindowViewModel vm
                 && vm.SelectedEntry is not null
                 && FocusManager?.GetFocusedElement() is not TextBox)
        {
            // Nicht löschen, während in einem Textfeld getippt wird (Suche/Kommentar)
            vm.DeleteEntryCommand.Execute(null); // null → nutzt SelectedEntry
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    private void OnEntryDoubleTapped(object? sender, TappedEventArgs e) =>
        (DataContext as MainWindowViewModel)?.CopySelectedCommand.Execute(null);

    private void OnCommentLostFocus(object? sender, RoutedEventArgs e) =>
        (DataContext as MainWindowViewModel)?.NotifyCommentChanged();

    private void OnAboutClick(object? sender, RoutedEventArgs e)
    {
        var updateService = App.Services.GetService<UpdateService>();
        new AboutWindow(updateService, DataContext as MainWindowViewModel).ShowDialog(this);
    }
}
