using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Klemmbrett.Models;
using Klemmbrett.Services;
using Xunit;

namespace Klemmbrett.Tests;

public class HistoryStorageServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "klemmbrett-test-" + Guid.NewGuid());

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    [Fact]
    public void SaveAndLoad_RoundtripsTextEntries()
    {
        var sut = new HistoryStorageService(_dir, new TestProtector());
        var entries = new IClipboardEntry[]
        {
            new TextClipboardEntry("neuester"),
            new TextClipboardEntry("älter", DateTimeOffset.Now.AddDays(-3))
        };

        sut.SaveIndex(entries);
        var loaded = new HistoryStorageService(_dir, new TestProtector()).Load();

        loaded.Should().HaveCount(2);
        loaded.OfType<TextClipboardEntry>().Select(t => t.Text)
            .Should().ContainInOrder("neuester", "älter");
    }

    [Fact]
    public void Load_DropsEntriesOlderThan30Days()
    {
        var sut = new HistoryStorageService(_dir, new TestProtector());
        sut.SaveIndex([
            new TextClipboardEntry("frisch"),
            new TextClipboardEntry("uralt", DateTimeOffset.Now.AddDays(-31))
        ]);

        // SaveIndex filtert bereits — zusätzlich prüft Load erneut
        var loaded = new HistoryStorageService(_dir, new TestProtector()).Load();
        loaded.Should().ContainSingle()
            .Which.Should().BeOfType<TextClipboardEntry>()
            .Which.Text.Should().Be("frisch");
    }

    [Fact]
    public void SaveIndex_MovesOrphanImageToTrashInsteadOfDeleting()
    {
        var sut = new HistoryStorageService(_dir, new TestProtector());
        var orphan = Path.Combine(_dir, "images", "DEADBEEF.png");
        File.WriteAllBytes(orphan, [1, 2, 3]);

        sut.SaveIndex([new TextClipboardEntry("nur text")]);

        File.Exists(orphan).Should().BeFalse("das Bild darf nicht mehr im Live-Ordner liegen");
        Directory.EnumerateFiles(Path.Combine(_dir, "images.trash"))
            .Should().ContainSingle(f => f.EndsWith("DEADBEEF.png", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Load_WithMissingIndex_ReturnsEmpty()
        => new HistoryStorageService(_dir, new TestProtector()).Load().Should().BeEmpty();

    [Fact]
    public void SaveIndex_WithSecretEntry_WritesEncryptedNotPlaintext()
    {
        var sut = new HistoryStorageService(_dir, new TestProtector());
        // Passwort-artiger String, den SecretDetector als Secret erkennt (Länge + Zeichenklassen inkl. Ziffer)
        var secret = "Hunter2!SuperGeheim9x";
        sut.SaveIndex([new TextClipboardEntry(secret)]);

        var json = File.ReadAllText(Path.Combine(_dir, "history.json"));
        json.Should().NotContain(secret, "Klartext-Passwort darf nicht auf der Platte landen");
        json.Should().Contain("TextEnc", "Secret muss im TextEnc-Feld verschlüsselt persistiert werden");

        var loaded = new HistoryStorageService(_dir, new TestProtector()).Load();
        loaded.OfType<TextClipboardEntry>().Single().Text.Should().Be(secret);
    }

    [Fact]
    public void Load_LegacyPlaintextIndex_StillReadable()
    {
        // Historische history.json vor der Inline-Verschlüsselung: nur Text-Feld, kein TextEnc
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "history.json"),
            $$"""[{"Type":"text","CapturedAt":"{{DateTimeOffset.Now:O}}","Text":"altbestand","Hash":null,"Pinned":false}]""");

        var loaded = new HistoryStorageService(_dir, new TestProtector()).Load();

        loaded.OfType<TextClipboardEntry>().Single().Text.Should().Be("altbestand");
    }
}
