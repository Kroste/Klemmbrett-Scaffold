using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
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

    // Referenz halten, damit TrayController/TrayIcon nicht vom GC eingesammelt werden.
    private TrayController? _trayController;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ClipboardHistoryService>();
        services.AddSingleton<ClipboardMonitorService>();
        services.AddSingleton<UpdateService>();
        services.AddTransient<MainWindowViewModel>();
        Services = services.BuildServiceProvider();
        Log.Debug("DI-Container aufgebaut ({Count} Registrierungen)", services.Count);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainWindowViewModel>()
            };
            desktop.MainWindow = window;

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
        }

        base.OnFrameworkInitializationCompleted();
    }
}
