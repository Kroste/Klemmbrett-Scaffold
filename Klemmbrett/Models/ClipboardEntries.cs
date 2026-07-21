using System;
using System.Security.Cryptography;
using Avalonia.Media.Imaging;

namespace Klemmbrett.Models;

/// <summary>Ein Eintrag im Zwischenablage-Verlauf (Text oder Bild).</summary>
public interface IClipboardEntry
{
    /// <summary>Schlüssel für Deduplizierung (gleicher Inhalt = gleicher Key).</summary>
    string DedupeKey { get; }

    /// <summary>Zeitpunkt der (letzten) Erfassung — Basis für Tagesfilter und 30-Tage-Aufbewahrung.</summary>
    DateTimeOffset CapturedAt { get; }

    /// <summary>Angeheftet: bleibt oben und ist von der 30-Tage-Aufbewahrung ausgenommen.</summary>
    bool IsPinned { get; set; }
}

public sealed class TextClipboardEntry : IClipboardEntry
{
    public TextClipboardEntry(string text, DateTimeOffset? capturedAt = null)
    {
        Text = text;
        CapturedAt = capturedAt ?? DateTimeOffset.Now;
        var firstLine = text.AsSpan().TrimStart();
        var nl = firstLine.IndexOfAny('\r', '\n');
        if (nl >= 0) firstLine = firstLine[..nl];
        Preview = firstLine.Length > 120 ? string.Concat(firstLine[..120], "…") : firstLine.ToString();
    }

    /// <summary>Vollständiger Text (wird beim Zurückkopieren verwendet).</summary>
    public string Text { get; }

    /// <summary>Einzeilige Kurzform für die Listenanzeige.</summary>
    public string Preview { get; }

    public DateTimeOffset CapturedAt { get; }

    public bool IsPinned { get; set; }

    public string DedupeKey => "T:" + Text;
}

public sealed class ImageClipboardEntry : IClipboardEntry
{
    public ImageClipboardEntry(Bitmap bitmap, string contentHash, DateTimeOffset? capturedAt = null)
    {
        Bitmap = bitmap;
        ContentHash = contentHash;
        CapturedAt = capturedAt ?? DateTimeOffset.Now;
        Info = $"Bild {bitmap.PixelSize.Width}×{bitmap.PixelSize.Height}";
    }

    public Bitmap Bitmap { get; }
    public string ContentHash { get; }
    public string Info { get; }
    public DateTimeOffset CapturedAt { get; }

    public bool IsPinned { get; set; }

    public string DedupeKey => "I:" + ContentHash;

    /// <summary>SHA-256 über die PNG-Bytes — identifiziert Bildinhalte über Poll-Zyklen hinweg.</summary>
    public static string ComputeHash(byte[] pngBytes) => Convert.ToHexString(SHA256.HashData(pngBytes));
}
