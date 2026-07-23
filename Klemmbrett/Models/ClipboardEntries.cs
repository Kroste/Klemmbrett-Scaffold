using System;
using System.Security.Cryptography;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

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

    /// <summary>Freitext-Notiz zum Eintrag (durchsuchbar, wird persistiert).</summary>
    string? Comment { get; set; }
}

/// <summary>
/// Gemeinsame Basis für alle Eintragsarten. ObservableObject, damit Kommentar,
/// Pin und Aufdecken live an die Liste binden.
/// </summary>
public abstract partial class ClipboardEntry : ObservableObject, IClipboardEntry
{
    protected ClipboardEntry(DateTimeOffset capturedAt) => CapturedAt = capturedAt;

    public abstract string DedupeKey { get; }

    public DateTimeOffset CapturedAt { get; }

    [ObservableProperty]
    private bool _isPinned;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasComment))]
    private string? _comment;

    /// <summary>Nur-Sitzung: steuert den ausgeklappten Kommentar-Editor (nicht persistiert).</summary>
    [ObservableProperty]
    private bool _isEditingComment;

    /// <summary>True, wenn ein nicht-leerer Kommentar hinterlegt ist (steuert Anzeige/Icon).</summary>
    public bool HasComment => !string.IsNullOrWhiteSpace(Comment);
}

public sealed partial class TextClipboardEntry : ClipboardEntry
{
    public TextClipboardEntry(string text, DateTimeOffset? capturedAt = null)
        : base(capturedAt ?? DateTimeOffset.Now)
    {
        Text = text;
        var firstLine = text.AsSpan().TrimStart();
        var nl = firstLine.IndexOfAny('\r', '\n');
        if (nl >= 0) firstLine = firstLine[..nl];
        Preview = firstLine.Length > 120 ? string.Concat(firstLine[..120], "…") : firstLine.ToString();
        IsSecret = SecretDetector.LooksLikeSecret(text);
    }

    /// <summary>Vollständiger Text (wird beim Zurückkopieren verwendet).</summary>
    public string Text { get; }

    /// <summary>Einzeilige Kurzform für die Listenanzeige.</summary>
    public string Preview { get; }

    /// <summary>Sieht wie ein Passwort/Secret aus → in der Liste maskiert.</summary>
    public bool IsSecret { get; }

    /// <summary>Nur-Sitzung: erkanntes Secret vorübergehend im Klartext zeigen.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayPreview))]
    private bool _isRevealed;

    /// <summary>Was in der Liste steht: bei erkannten Secrets maskiert, bis aufgedeckt.</summary>
    public string DisplayPreview => IsSecret && !IsRevealed ? "•••••••• (Passwort verborgen)" : Preview;

    public override string DedupeKey => "T:" + Text;
}

public sealed class ImageClipboardEntry : ClipboardEntry
{
    public ImageClipboardEntry(Bitmap bitmap, string contentHash, DateTimeOffset? capturedAt = null)
        : base(capturedAt ?? DateTimeOffset.Now)
    {
        Bitmap = bitmap;
        ContentHash = contentHash;
        Info = $"Bild {bitmap.PixelSize.Width}×{bitmap.PixelSize.Height}";
    }

    public Bitmap Bitmap { get; }
    public string ContentHash { get; }
    public string Info { get; }

    public override string DedupeKey => "I:" + ContentHash;

    /// <summary>SHA-256 über die PNG-Bytes — identifiziert Bildinhalte über Poll-Zyklen hinweg.</summary>
    public static string ComputeHash(byte[] pngBytes) => Convert.ToHexString(SHA256.HashData(pngBytes));
}
