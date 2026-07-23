using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace Klemmbrett.Services;

/// <summary>
/// Räumt <see cref="HistoryStorageService.TrashDirectory"/> im Hintergrund auf:
/// nur Dateien, die älter als <see cref="MinAge"/> sind, und gedrosselt mit einer
/// Pause zwischen den Löschungen. Ziel: kein „Wiper"-Verhalten (viele Löschungen
/// in kurzer Zeit), auf das Verhaltens-AV anschlägt. Läuft einmalig beim App-Start.
/// </summary>
public sealed class TrashCleanupService
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private readonly HistoryStorageService _storage;

    /// <summary>Nur Dateien löschen, die mindestens so alt sind.</summary>
    public TimeSpan MinAge { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>Pause zwischen zwei Löschungen — hält den Delete-Fluss unter AV-Verdachtsschwellen.</summary>
    public TimeSpan Throttle { get; init; } = TimeSpan.FromMilliseconds(150);

    public TrashCleanupService(HistoryStorageService storage)
    {
        _storage = storage;
    }

    /// <summary>Feuert eine Hintergrund-Aufräumung ab (nicht awaitbar — fire-and-forget).</summary>
    public void StartInBackground(CancellationToken cancellation = default)
    {
        _ = Task.Run(() => RunAsync(cancellation), cancellation);
    }

    /// <summary>Testbarer Einstiegspunkt: räumt synchron im aufrufenden Thread.</summary>
    public async Task RunAsync(CancellationToken cancellation = default)
    {
        var dir = _storage.TrashDirectory;
        if (!Directory.Exists(dir))
            return;

        var threshold = DateTime.UtcNow - MinAge;
        var deleted = 0;
        var skipped = 0;

        foreach (var file in Directory.EnumerateFiles(dir))
        {
            if (cancellation.IsCancellationRequested)
                break;

            try
            {
                if (File.GetLastWriteTimeUtc(file) > threshold)
                {
                    skipped++;
                    continue;
                }
                File.Delete(file);
                deleted++;
                if (Throttle > TimeSpan.Zero)
                    await Task.Delay(Throttle, cancellation).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Log.Warn(ex, "Trash-Datei konnte nicht gelöscht werden: {File}", file);
            }
        }

        if (deleted > 0 || skipped > 0)
            Log.Info("Trash aufgeräumt: {Deleted} gelöscht, {Skipped} noch zu jung", deleted, skipped);
    }
}
