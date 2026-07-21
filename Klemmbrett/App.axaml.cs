using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
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

    /// <summary>True, sobald wirklich beendet wird (Tray-Menü „Beenden") —
    /// solange false, minimiert das Schließen-X nur in den Tray.</summary>
    public static bool IsExiting { get; private set; }

    private TrayIcon? _trayIcon;

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
            // Ohne das würde das Verstecken des letzten Fensters die App beenden
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            desktop.MainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainWindowViewModel>()
            };

            SetupTrayIcon(desktop);
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>Kroste-Tray-Muster (vgl. Checkmk Cockpit): Klick zeigt das Fenster,
    /// Menü mit Anzeigen/Beenden; Schließen minimiert nur in den Tray.</summary>
    private void SetupTrayIcon(IClassicDesktopStyleApplicationLifetime desktop)
    {
        try
        {
            var showItem = new NativeMenuItem("Anzeigen");
            showItem.Click += (_, _) => ShowMainWindow(desktop);
            var exitItem = new NativeMenuItem("Beenden");
            exitItem.Click += (_, _) => ExitApplication(desktop);

            _trayIcon = new TrayIcon
            {
                Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://Klemmbrett/Assets/Klemmbrett.png"))),
                ToolTipText = "Klemmbrett",
                Menu = new NativeMenu { Items = { showItem, exitItem } }
            };
            _trayIcon.Clicked += (_, _) => ShowMainWindow(desktop);
            TrayIcon.SetIcons(this, [_trayIcon]);
            Log.Info("Tray-Icon eingerichtet");
        }
        catch (Exception ex)
        {
            // Kein Tray verfuegbar (z.B. minimale Desktops): App bleibt nutzbar,
            // Schliessen beendet dann regulaer.
            Log.Warn(ex, "Tray-Icon nicht verfügbar — Schließen beendet die App");
            _trayIcon = null;
        }
    }

    /// <summary>Vom MainWindow beim Schließen gefragt: in den Tray statt beenden?</summary>
    public static bool ShouldMinimizeToTray => !IsExiting && ((App)Current!)._trayIcon is not null;

    private static void ShowMainWindow(IClassicDesktopStyleApplicationLifetime desktop)
    {
        if (desktop.MainWindow is not { } window)
            return;
        Log.Debug("Nutzeraktion: Fenster aus Tray anzeigen");
        window.Show();
        window.WindowState = WindowState.Normal;
        window.Activate();
    }

    private void ExitApplication(IClassicDesktopStyleApplicationLifetime desktop)
    {
        Log.Info("Nutzeraktion: Beenden über Tray-Menü");
        IsExiting = true;
        _trayIcon?.Dispose();
        desktop.Shutdown();
    }
}
