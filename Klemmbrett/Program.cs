using System;
using Avalonia;
using Avalonia.Media;
using Klemmbrett.Logging;
using Klemmbrett.Services;
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

            // Nur eine Instance zulassen: läuft schon eine, holen wir deren Fenster in den Vordergrund
            // und beenden uns sofort — kein Avalonia-Start, keine doppelte Clipboard-Überwachung,
            // keine konkurrierenden Schreibzugriffe auf history.json.
            var guard = new SingleInstanceGuard();
            if (!guard.TryClaim())
            {
                guard.NotifyPrimary();
                guard.Dispose();
                Log.Info("Klemmbrett läuft bereits — vorhandene Instance aktiviert, beende zweiten Start");
                return 0;
            }
            App.PendingGuard = guard;

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

    public static AppBuilder BuildAvaloniaApp()
    {
        // Farb-Emojis (🧹 …) brauchen einen expliziten Fallback auf den
        // Color-Emoji-Font des Systems, sonst erscheinen sie einfarbig.
        var emojiFont = OperatingSystem.IsWindows() ? "Segoe UI Emoji"
            : OperatingSystem.IsMacOS() ? "Apple Color Emoji"
            : "Noto Color Emoji";

        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .With(new FontManagerOptions
            {
                // WithInterFont() setzt die Default-Familie ueber FontManagerOptions;
                // da wir die Options ersetzen, muss Inter erneut angegeben werden:
                DefaultFamilyName = "fonts:Inter#Inter",
                FontFallbacks = [new FontFallback { FontFamily = new FontFamily(emojiFont) }]
            })
            .LogToTrace();
    }
}
