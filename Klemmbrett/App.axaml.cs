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
            desktop.MainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainWindowViewModel>()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
