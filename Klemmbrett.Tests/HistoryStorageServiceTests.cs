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
        var sut = new HistoryStorageService(_dir);
        var entries = new IClipboardEntry[]
        {
            new TextClipboardEntry("neuester"),
            new TextClipboardEntry("älter", DateTimeOffset.Now.AddDays(-3))
        };

        sut.SaveIndex(entries);
        var loaded = new HistoryStorageService(_dir).Load();

        loaded.Should().HaveCount(2);
        loaded.OfType<TextClipboardEntry>().Select(t => t.Text)
            .Should().ContainInOrder("neuester", "älter");
    }

    [Fact]
    public void Load_DropsEntriesOlderThan30Days()
    {
        var sut = new HistoryStorageService(_dir);
        sut.SaveIndex([
            new TextClipboardEntry("frisch"),
            new TextClipboardEntry("uralt", DateTimeOffset.Now.AddDays(-31))
        ]);

        // SaveIndex filtert bereits — zusätzlich prüft Load erneut
        var loaded = new HistoryStorageService(_dir).Load();
        loaded.Should().ContainSingle()
            .Which.Should().BeOfType<TextClipboardEntry>()
            .Which.Text.Should().Be("frisch");
    }

    [Fact]
    public void SaveIndex_RemovesOrphanImageFiles()
    {
        var sut = new HistoryStorageService(_dir);
        var orphan = Path.Combine(_dir, "images", "DEADBEEF.png");
        File.WriteAllBytes(orphan, [1, 2, 3]);

        sut.SaveIndex([new TextClipboardEntry("nur text")]);

        File.Exists(orphan).Should().BeFalse();
    }

    [Fact]
    public void Load_WithMissingIndex_ReturnsEmpty()
        => new HistoryStorageService(_dir).Load().Should().BeEmpty();
}
