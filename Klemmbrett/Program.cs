using System;
using Avalonia;
using Klemmbrett.Logging;
using NLog;

namespace Klemmbrett;

internal static class Program
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    [STAThread]
    public static int Main(string[] args)
    {
        // Masked-Renderer registrieren, BEVOR der erste Logger konfiguriert wird
        MaskedLayoutRenderer.Register();
        LogManager.Setup().LoadConfigurationFromFile("nlog.config");

        // GlobalExceptionHandler: nichts stürzt still ab
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Log.Fatal(e.ExceptionObject as Exception, "Unbehandelte AppDomain-Exception");
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log.Error(e.Exception, "Unbeobachtete Task-Exception");
            e.SetObserved();
        };

        try
        {
            Log.Info("Klemmbrett startet (Version {Version})",
                typeof(Program).Assembly.GetName().Version);
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Fataler Fehler beim App-Start");
            return 1;
        }
        finally
        {
            Log.Info("Klemmbrett beendet");
            LogManager.Shutdown();
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
