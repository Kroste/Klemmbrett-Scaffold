using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Avalonia.Threading;
using NLog;

namespace Klemmbrett.Services;

/// <summary>
/// Kroste-Tray-Muster (Referenz: Checkmk Cockpit TrayController):
/// Der Minimieren-Knopf legt das Fenster ins Tray (WindowState-Listener),
/// Klick/„Anzeigen" holt es zurück, das Schließen-✕ beendet regulär.
/// Der Aufrufer MUSS eine Referenz auf diese Instanz halten, sonst sammelt
/// der GC das Tray-Icon ein!
/// </summary>
public sealed class TrayController
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private readonly Application _app;
    private readonly Window _window;
    private TrayIcon _trayIcon = null!;
    private bool _restoreInProgress;

    public bool IsMinimizedToTray { get; private set; }

    public TrayController(Application app, Window window)
    {
        _app = app;
        _window = window;
        BuildTray();
        _window.PropertyChanged += OnWindowPropertyChanged;
        Log.Info("Tray-Icon eingerichtet (Minimieren legt ins Tray)");
    }

    private void BuildTray()
    {
        _trayIcon = new TrayIcon
        {
            Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://Klemmbrett/Assets/Klemmbrett.png"))),
            ToolTipText = "Klemmbrett — Zwischenablage-Verlauf",
            IsVisible = true,
            Menu = new NativeMenu()
        };

        var show = new NativeMenuItem("Anzeigen");
        show.Click += (_, _) => Restore();
        var exit = new NativeMenuItem("Beenden");
        exit.Click += (_, _) =>
        {
            Log.Info("Nutzeraktion: Beenden über Tray-Menü");
            (_app.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
        };

        _trayIcon.Menu.Items.Add(show);
        _trayIcon.Menu.Items.Add(new NativeMenuItemSeparator());
        _trayIcon.Menu.Items.Add(exit);
        _trayIcon.Clicked += (_, _) => Restore();

        TrayIcon.SetIcons(_app, [_trayIcon]);
    }

    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != Window.WindowStateProperty || _restoreInProgress)
            return;

        if ((WindowState)e.NewValue! == WindowState.Minimized)
            MinimizeToTray();
    }

    /// <summary>Legt das Fenster ins Tray (vom Minimieren-Knopf und vom Schließen-✕ genutzt).</summary>
    public void MinimizeToTray()
    {
        IsMinimizedToTray = true;
        _window.Hide(); // Hide() schliesst nicht — kein ShutdownMode-Umbau noetig
        Log.Debug("Ins Tray minimiert — Clipboard-Überwachung läuft weiter");
    }

    /// <summary>True, solange ein Tray verfügbar ist (Schließen darf dann ins Tray statt beenden).</summary>
    public bool IsAvailable => _trayIcon is not null;

    private void Restore()
    {
        // Guard + Post: das Setzen von WindowState.Normal feuert PropertyChanged
        // erneut — ohne Guard gäbe es eine Minimize/Restore-Schleife.
        Dispatcher.UIThread.Post(() =>
        {
            _restoreInProgress = true;
            _window.Show();
            _window.WindowState = WindowState.Normal;
            _window.Activate();
            IsMinimizedToTray = false;
            _restoreInProgress = false;
            Log.Debug("Fenster aus Tray wiederhergestellt");
        });
    }
}
