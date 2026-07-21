using System;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Klemmbrett.Services;
using NLog;

namespace Klemmbrett.Views;

public partial class AboutWindow : ChromeWindow
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    private const string GithubUrl = "https://github.com/Kroste/Klemmbrett-Scaffold";
    private const string BmcUrl = "https://buymeacoffee.com/kroste";

    private readonly UpdateService? _updateService;

    // Parameterloser Ctor für den XAML-Designer
    public AboutWindow() : this(null) { }

    public AboutWindow(UpdateService? updateService)
    {
        AvaloniaXamlLoader.Load(this);
        _updateService = updateService;

        var v = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
            ?? "0.0.0";
        this.FindControl<TextBlock>("VersionText")!.Text = $"Version {v}";
    }

    private async void OnCheckUpdateClick(object? sender, RoutedEventArgs e)
    {
        var status = this.FindControl<TextBlock>("UpdateStatus")!;
        var button = this.FindControl<Button>("CheckUpdateButton")!;
        if (_updateService is null)
            return;

        button.IsEnabled = false;
        status.IsVisible = true;
        status.Text = "Suche nach Updates…";
        try
        {
            var result = await _updateService.CheckForUpdateAsync();
            status.Text = result?.UpdateAvailable == true
                ? $"Version {result.Latest} verfügbar — im Hauptfenster aktualisieren."
                : "Du hast die aktuelle Version.";
            Log.Info("Manueller Update-Check: {Msg}", status.Text);
        }
        catch (Exception ex)
        {
            status.Text = "Update-Prüfung fehlgeschlagen (offline?).";
            Log.Warn(ex, "Manueller Update-Check fehlgeschlagen");
        }
        finally
        {
            button.IsEnabled = true;
        }
    }

    private void OnGithubClick(object? sender, RoutedEventArgs e) => Launch(GithubUrl);
    private void OnBmcClick(object? sender, RoutedEventArgs e) => Launch(BmcUrl);
    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private void Launch(string url)
    {
        try { TopLevel.GetTopLevel(this)?.Launcher.LaunchUriAsync(new Uri(url)); }
        catch (Exception ex) { Log.Warn(ex, "Link konnte nicht geöffnet werden: {Url}", url); }
    }
}
