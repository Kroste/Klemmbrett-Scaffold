using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Klemmbrett.Models;
using Klemmbrett.Services;
using Xunit;

namespace Klemmbrett.Tests;

public class DeleteEntryTests
{
    [Fact]
    public void Remove_DeletesMatchingEntry()
    {
        var sut = new ClipboardHistoryService();
        sut.Add(new TextClipboardEntry("a"));
        sut.Add(new TextClipboardEntry("b"));

        sut.Remove(sut.Entries.First(e => ((TextClipboardEntry)e).Text == "a"));

        sut.Entries.Should().ContainSingle()
            .Which.Should().BeOfType<TextClipboardEntry>()
            .Which.Text.Should().Be("b");
    }

    [Fact]
    public void Remove_UnknownEntry_LeavesHistoryUnchanged()
    {
        var sut = new ClipboardHistoryService();
        sut.Add(new TextClipboardEntry("bleibt"));

        sut.Remove(new TextClipboardEntry("nie hinzugefügt"));

        sut.Entries.Should().ContainSingle();
    }

    [Fact]
    public void Remove_AlsoWorksForPinnedEntries()
    {
        var sut = new ClipboardHistoryService();
        var pin = new TextClipboardEntry("wichtig") { IsPinned = true };
        sut.Add(pin);
        sut.Remove(pin);
        sut.Entries.Should().BeEmpty();
    }

    [Fact]
    public void SaveIndex_AfterDelete_RemovesOrphanImage()
    {
        var dir = Path.Combine(Path.GetTempPath(), "kb-del-" + Guid.NewGuid());
        try
        {
            var storage = new HistoryStorageService(dir);
            // Bilddatei simulieren + Index mit Bildeintrag
            var hash = "ABC123";
            File.WriteAllBytes(Path.Combine(dir, "images", hash + ".png"), [1, 2, 3]);
            // Nach dem "Löschen" enthält der Index das Bild nicht mehr:
            storage.SaveIndex([new TextClipboardEntry("nur text")]);
            File.Exists(Path.Combine(dir, "images", hash + ".png")).Should().BeFalse();
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }
}
