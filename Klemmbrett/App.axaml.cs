using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Klemmbrett.Services;
using Klemmbrett.ViewModels;
using Klemmbrett.Views;
using Microsoft.Extensions.DependencyInjection;
using NLog;

namespace Klemmbrett;

public class App : Application
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    public static IServiceProvider Services { get; private set; } = null!;

    /// <summary>
    /// Wird von <c>Program.Main</c> gesetzt, bevor Avalonia startet: der bereits
    /// beanspruchte Single-Instance-Guard. Die App übernimmt die Referenz in
    /// <see cref="OnFrameworkInitializationCompleted"/> und disposed sie beim Exit.
    /// </summary>
    public static SingleInstanceGuard? PendingGuard { get; set; }

    // Referenz halten, damit TrayController/TrayIcon nicht vom GC eingesammelt werden.
    private TrayController? _trayController;
    private SingleInstanceGuard? _instanceGuard;
    private Window? _mainWindow;

    /// <summary>Der aktive TrayController (null, wenn kein Tray verfügbar ist).</summary>
    public static TrayController? Tray => (Current as App)?._trayController;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISecretProtector, SecretProtector>();
        services.AddSingleton<ClipboardHistoryService>();
        services.AddSingleton<ClipboardMonitorService>();
        services.AddSingleton<HistoryStorageService>();
        services.AddSingleton<TrashCleanupService>();
        services.AddSingleton<UpdateService>();
        services.AddTransient<MainWindowViewModel>();
        Services = services.BuildServiceProvider();
        Log.Debug("DI-Container aufgebaut ({Count} Registrierungen)", services.Count);

        // Trash im Hintergrund throttled aufräumen — nur alte Reste, kein Delete-Storm.
        Services.GetRequiredService<TrashCleanupService>().StartInBackground();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainWindowViewModel>()
            };
            _mainWindow = window;
            desktop.MainWindow = window;
            desktop.Exit += (_, _) =>
            {
                (window.DataContext as MainWindowViewModel)?.PersistOnExit();
                _instanceGuard?.Dispose();
            };

            try
            {
                _trayController = new TrayController(this, window);
            }
            catch (Exception ex)
            {
                // Kein Tray verfuegbar (minimale Desktops, DBus-Probleme):
                // Minimieren verhaelt sich dann normal, App bleibt voll nutzbar.
                Log.Warn(ex, "Tray-Icon nicht verfügbar — Minimieren bleibt normal");
            }

            // Single-Instance-Guard aus Program.cs übernehmen und ans Aktivierungssignal binden.
            _instanceGuard = PendingGuard;
            PendingGuard = null;
            if (_instanceGuard is not null)
                _instanceGuard.ActivationRequested += OnSecondInstanceRequested;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnSecondInstanceRequested()
    {
        // Callback kommt aus dem ThreadPool — UI-Zugriffe müssen auf den Dispatcher.
        Dispatcher.UIThread.Post(() =>
        {
            Log.Info("Zweite Instance angefordert — hole Fenster in den Vordergrund");
            if (_trayController is { } tray)
            {
                tray.Restore();
                return;
            }
            if (_mainWindow is { } window)
            {
                window.Show();
                window.WindowState = WindowState.Normal;
                window.Activate();
            }
        });
    }
}
