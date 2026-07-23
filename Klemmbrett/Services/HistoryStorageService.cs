using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Avalonia.Media.Imaging;
using Klemmbrett.Models;
using NLog;

namespace Klemmbrett.Services;

/// <summary>
/// Persistiert den Verlauf (30 Tage) im AppData-Verzeichnis:
/// history.json als Index (atomar via tmp+move, Amtsschimmel-Muster),
/// Bilder als PNG-Dateien unter images/&lt;hash&gt;.png. Beim Laden und Speichern
/// werden abgelaufene Einträge und verwaiste Bilddateien entfernt.
///
/// <para>Verwaiste Bilder werden NICHT direkt gelöscht, sondern nach
/// <c>images.trash/</c> verschoben — verhindert das „Wiper"-Verhaltensmuster,
/// auf das Verhaltens-AV (z. B. Trend Micro) anschlägt. Der Trash wird von
/// <see cref="TrashCleanupService"/> throttled und zeitversetzt aufgeräumt.</para>
///
/// <para>Als Secret erkannte Text-Einträge werden inline via <see cref="ISecretProtector"/>
/// verschlüsselt (Feld <c>TextEnc</c>) — nicht die ganze Datei. Alter Klartext-JSON bleibt
/// lesbar und wird beim nächsten Speichern automatisch migriert.</para>
/// </summary>
public sealed class HistoryStorageService
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _dir;
    private readonly string _imagesDir;
    private readonly string _trashDir;
    private readonly string _indexPath;
    private readonly ISecretProtector _protector;

    private sealed record StoredEntry(
        string Type,
        DateTimeOffset CapturedAt,
        string? Text,
        string? TextEnc,
        string? Hash,
        bool Pinned = false,
        string? Comment = null);

    public HistoryStorageService(ISecretProtector protector)
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Klemmbrett"), protector) { }

    /// <summary>Testbarer Konstruktor mit explizitem Verzeichnis.</summary>
    public HistoryStorageService(string directory, ISecretProtector protector)
    {
        _dir = directory;
        _imagesDir = Path.Combine(_dir, "images");
        _trashDir = Path.Combine(_dir, "images.trash");
        _indexPath = Path.Combine(_dir, "history.json");
        _protector = protector;
        Directory.CreateDirectory(_imagesDir);
        Directory.CreateDirectory(_trashDir);
    }

    /// <summary>Absoluter Pfad zum Trash-Ordner — <see cref="TrashCleanupService"/> räumt hier auf.</summary>
    public string TrashDirectory => _trashDir;

    /// <summary>Lädt den Verlauf (neueste zuerst); abgelaufene Einträge werden verworfen.</summary>
    public List<IClipboardEntry> Load()
    {
        var result = new List<IClipboardEntry>();
        if (!File.Exists(_indexPath))
        {
            Log.Info("Kein gespeicherter Verlauf gefunden ({Path})", _indexPath);
            return result;
        }

        try
        {
            var stored = JsonSerializer.Deserialize<List<StoredEntry>>(File.ReadAllText(_indexPath)) ?? [];
            var now = DateTimeOffset.Now;
            int expired = 0, broken = 0;

            foreach (var s in stored)
            {
                var entry = Restore(s);
                if (entry is null) { broken++; continue; }
                if (HistoryDayFilter.IsExpired(entry, now)) { expired++; continue; }
                result.Add(entry);
            }

            Log.Info("Verlauf geladen: {Count} Einträge ({Expired} abgelaufen, {Broken} defekt verworfen)",
                result.Count, expired, broken);
        }
        catch (Exception ex)
        {
            // Defekter Index: Backup statt Löschung (Amtsschimmel-Muster), leer starten
            Log.Error(ex, "Verlauf konnte nicht geladen werden — lege Backup an und starte leer");
            try { File.Copy(_indexPath, _indexPath + ".broken", overwrite: true); } catch { /* best effort */ }
        }

        return result;
    }

    private IClipboardEntry? Restore(StoredEntry s)
    {
        try
        {
            switch (s.Type)
            {
                case "text":
                    var text = ResolveText(s);
                    return text is null
                        ? null
                        : new TextClipboardEntry(text, s.CapturedAt) { IsPinned = s.Pinned, Comment = s.Comment };
                case "image" when s.Hash is not null:
                    var path = ImagePath(s.Hash);
                    if (!File.Exists(path)) return null;
                    using (var fs = File.OpenRead(path))
                        return new ImageClipboardEntry(new Bitmap(fs), s.Hash, s.CapturedAt) { IsPinned = s.Pinned, Comment = s.Comment };
                default:
                    return null;
            }
        }
        catch (Exception ex)
        {
            Log.Warn(ex, "Eintrag ({Type}) konnte nicht wiederhergestellt werden", s.Type);
            return null;
        }
    }

    private string? ResolveText(StoredEntry s)
    {
        // Neu: verschlüsseltes Feld hat Vorrang.
        if (_protector.IsProtected(s.TextEnc))
        {
            try
            {
                return _protector.Unprotect(s.TextEnc!);
            }
            catch (Exception ex)
            {
                // Anderer Nutzer/DPAPI-Schlüssel weg/Chiffrat kaputt: verwerfen statt crashen.
                Log.Warn(ex, "Verschlüsselter Eintrag konnte nicht entschlüsselt werden — wird verworfen");
                return null;
            }
        }
        // Legacy: Klartext-Feld (auch für erkannte Secrets, wird beim nächsten Save migriert).
        return s.Text;
    }

    /// <summary>Legt die PNG-Datei eines Bild-Eintrags ab (einmalig pro Hash).</summary>
    public void EnsureImageSaved(ImageClipboardEntry entry)
    {
        var path = ImagePath(entry.ContentHash);
        if (File.Exists(path))
            return;
        try
        {
            using var fs = File.Create(path);
            entry.Bitmap.Save(fs, PngBitmapEncoderOptions.Default);
            Log.Debug("Bild persistiert: {File}", Path.GetFileName(path));
        }
        catch (Exception ex)
        {
            Log.Warn(ex, "Bild konnte nicht persistiert werden");
        }
    }

    /// <summary>Schreibt den Index atomar (tmp + move) und schiebt verwaiste Bilder in den Trash.</summary>
    public void SaveIndex(IReadOnlyList<IClipboardEntry> entries)
    {
        try
        {
            var now = DateTimeOffset.Now;
            var kept = entries.Where(e => !HistoryDayFilter.IsExpired(e, now)).ToList();
            var stored = kept.Select(ToStoredEntry).Where(s => s is not null).ToList();

            var tmp = _indexPath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(stored, JsonOpts));
            File.Move(tmp, _indexPath, overwrite: true);
            Log.Debug("Verlauf gespeichert: {Count} Einträge", stored.Count);

            MoveOrphanImagesToTrash(kept);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Verlauf konnte nicht gespeichert werden");
        }
    }

    private StoredEntry? ToStoredEntry(IClipboardEntry entry) => entry switch
    {
        TextClipboardEntry t when t.IsSecret =>
            new StoredEntry("text", t.CapturedAt, Text: null, TextEnc: _protector.Protect(t.Text), Hash: null, t.IsPinned, t.Comment),
        TextClipboardEntry t =>
            new StoredEntry("text", t.CapturedAt, Text: t.Text, TextEnc: null, Hash: null, t.IsPinned, t.Comment),
        ImageClipboardEntry i =>
            new StoredEntry("image", i.CapturedAt, Text: null, TextEnc: null, Hash: i.ContentHash, i.IsPinned, i.Comment),
        _ => null
    };

    /// <summary>
    /// Verschiebt verwaiste PNGs nach <c>images.trash/</c> — kein Massenlöschen als Reaktion
    /// auf User-Aktionen. Der Trash wird von <see cref="TrashCleanupService"/> zeitversetzt geräumt.
    /// </summary>
    private void MoveOrphanImagesToTrash(IReadOnlyList<IClipboardEntry> kept)
    {
        var referenced = kept.OfType<ImageClipboardEntry>()
            .Select(i => i.ContentHash)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var file in Directory.EnumerateFiles(_imagesDir, "*.png"))
        {
            if (referenced.Contains(Path.GetFileNameWithoutExtension(file)))
                continue;
            try
            {
                var target = Path.Combine(_trashDir,
                    $"{DateTime.UtcNow:yyyyMMdd-HHmmssfff}-{Path.GetFileName(file)}");
                File.Move(file, target, overwrite: true);
                Log.Debug("Verwaistes Bild in den Trash verschoben: {File}", Path.GetFileName(file));
            }
            catch (Exception ex)
            {
                Log.Warn(ex, "Verwaistes Bild konnte nicht verschoben werden: {File}", file);
            }
        }
    }

    private string ImagePath(string hash) => Path.Combine(_imagesDir, hash + ".png");
}
