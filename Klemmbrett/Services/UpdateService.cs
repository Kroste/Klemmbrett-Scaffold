using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace Klemmbrett.Services;

/// <summary>
/// Update-Check gegen GitHub Releases (Kroste-Standard, siehe autoupdate-Referenz):
/// proxy-aware, nicht blockierend, max. 1 Check pro App-Start, nie silent installieren.
/// </summary>
public sealed class UpdateService
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    private const string ReleasesUrl = "https://api.github.com/repos/Kroste/Klemmbrett/releases/latest";

    private readonly HttpClient _http;
    private UpdateCheckResult? _cached;

    public UpdateService()
    {
        // System-Proxy + Negotiate: läuft identisch auf Arbeitslaptop und Bazzite
        var handler = new HttpClientHandler
        {
            Proxy = WebRequest.DefaultWebProxy,
            DefaultProxyCredentials = CredentialCache.DefaultCredentials
        };
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Klemmbrett-UpdateCheck"); // GitHub-API-Pflicht
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    }

    public static Version CurrentVersion =>
        ParseVersion(Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion)
        ?? new Version(0, 0, 0);

    /// <summary>Tag wie "v1.2.3", "1.2.3+abc123" oder "1.2.3-alpha.1" → Version. Null bei Müll.</summary>
    public static Version? ParseVersion(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return null;
        var s = tag.Trim().TrimStart('v', 'V');
        var cut = s.IndexOfAny(['+', '-']);
        if (cut >= 0) s = s[..cut];
        return Version.TryParse(s, out var v) ? v : null;
    }

    /// <summary>Fehler werden nur geloggt (Warn) — offline/Proxy darf die App nicht stören.</summary>
    public async Task<UpdateCheckResult?> CheckForUpdateAsync(CancellationToken ct = default)
    {
        if (_cached is not null) return _cached; // max. 1 echter Check pro App-Start

        var sw = Stopwatch.StartNew();
        try
        {
            Log.Debug("Update-Check: {Url}", ReleasesUrl);
            using var response = await _http.GetAsync(ReleasesUrl, ct);
            response.EnsureSuccessStatusCode();

            using var json = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(ct));
            var tag = json.RootElement.GetProperty("tag_name").GetString();
            var htmlUrl = json.RootElement.GetProperty("html_url").GetString() ?? "";

            var latest = ParseVersion(tag);
            if (latest is null)
            {
                Log.Warn("Update-Check: Tag '{Tag}' nicht parsebar", tag);
                return null;
            }

            _cached = new UpdateCheckResult(CurrentVersion, latest, latest > CurrentVersion, htmlUrl);
            Log.Info("Update-Check fertig in {Ms} ms: aktuell {Current}, neueste {Latest}, Update: {Available}",
                sw.ElapsedMilliseconds, _cached.Current, _cached.Latest, _cached.UpdateAvailable);
            return _cached;
        }
        catch (Exception ex)
        {
            Log.Warn(ex, "Update-Check fehlgeschlagen nach {Ms} ms (offline/Proxy?)", sw.ElapsedMilliseconds);
            return null;
        }
    }
}

public sealed record UpdateCheckResult(Version Current, Version Latest, bool UpdateAvailable, string ReleaseUrl);
