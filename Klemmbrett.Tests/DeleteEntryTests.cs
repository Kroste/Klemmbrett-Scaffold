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
    public void SaveIndex_AfterDelete_MovesOrphanImageToTrash()
    {
        var dir = Path.Combine(Path.GetTempPath(), "kb-del-" + Guid.NewGuid());
        try
        {
            var storage = new HistoryStorageService(dir, new TestProtector());
            // Bilddatei simulieren + Index mit Bildeintrag
            var hash = "ABC123";
            var original = Path.Combine(dir, "images", hash + ".png");
            File.WriteAllBytes(original, [1, 2, 3]);
            // Nach dem "Löschen" ist die Datei nicht mehr im Live-Ordner, aber im Trash —
            // wichtig für AV: kein Sofortlöschen aus einer User-Aktion.
            storage.SaveIndex([new TextClipboardEntry("nur text")]);

            File.Exists(original).Should().BeFalse();
            Directory.EnumerateFiles(Path.Combine(dir, "images.trash"))
                .Should().ContainSingle(f => f.Contains(hash, StringComparison.OrdinalIgnoreCase));
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }
}
