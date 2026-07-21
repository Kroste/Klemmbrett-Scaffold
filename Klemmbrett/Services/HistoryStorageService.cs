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
/// </summary>
public sealed class HistoryStorageService
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private readonly string _dir;
    private readonly string _imagesDir;
    private readonly string _indexPath;

    private sealed record StoredEntry(string Type, DateTimeOffset CapturedAt, string? Text, string? Hash, bool Pinned = false);

    public HistoryStorageService()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Klemmbrett")) { }

    /// <summary>Testbarer Konstruktor mit explizitem Verzeichnis.</summary>
    public HistoryStorageService(string directory)
    {
        _dir = directory;
        _imagesDir = Path.Combine(_dir, "images");
        _indexPath = Path.Combine(_dir, "history.json");
        Directory.CreateDirectory(_imagesDir);
    }

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
                case "text" when s.Text is not null:
                    return new TextClipboardEntry(s.Text, s.CapturedAt) { IsPinned = s.Pinned };
                case "image" when s.Hash is not null:
                    var path = ImagePath(s.Hash);
                    if (!File.Exists(path)) return null;
                    using (var fs = File.OpenRead(path))
                        return new ImageClipboardEntry(new Bitmap(fs), s.Hash, s.CapturedAt) { IsPinned = s.Pinned };
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

    /// <summary>Schreibt den Index atomar (tmp + move) und räumt Verwaistes auf.</summary>
    public void SaveIndex(IReadOnlyList<IClipboardEntry> entries)
    {
        try
        {
            var now = DateTimeOffset.Now;
            var kept = entries.Where(e => !HistoryDayFilter.IsExpired(e, now)).ToList();
            var stored = kept.Select(e => e switch
            {
                TextClipboardEntry t => new StoredEntry("text", t.CapturedAt, t.Text, null, t.IsPinned),
                ImageClipboardEntry i => new StoredEntry("image", i.CapturedAt, null, i.ContentHash, i.IsPinned),
                _ => null
            }).Where(s => s is not null).ToList();

            var tmp = _indexPath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(stored, JsonOpts));
            File.Move(tmp, _indexPath, overwrite: true);
            Log.Debug("Verlauf gespeichert: {Count} Einträge", stored.Count);

            CleanupOrphanImages(kept);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Verlauf konnte nicht gespeichert werden");
        }
    }

    private void CleanupOrphanImages(IReadOnlyList<IClipboardEntry> kept)
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
                File.Delete(file);
                Log.Debug("Verwaistes Bild gelöscht: {File}", Path.GetFileName(file));
            }
            catch (Exception ex)
            {
                Log.Warn(ex, "Verwaistes Bild konnte nicht gelöscht werden: {File}", file);
            }
        }
    }

    private string ImagePath(string hash) => Path.Combine(_imagesDir, hash + ".png");
}
