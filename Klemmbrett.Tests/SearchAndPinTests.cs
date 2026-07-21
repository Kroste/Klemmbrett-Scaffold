using System;
using System.Linq;
using FluentAssertions;
using Klemmbrett.Models;
using Klemmbrett.Services;
using Xunit;

namespace Klemmbrett.Tests;

public class SearchAndPinTests
{
    [Fact]
    public void PinnedEntry_IsNeverExpired()
    {
        var now = DateTimeOffset.Now;
        var pinned = new TextClipboardEntry("wichtig", now.AddDays(-100)) { IsPinned = true };
        var normal = new TextClipboardEntry("egal", now.AddDays(-100));

        HistoryDayFilter.IsExpired(pinned, now).Should().BeFalse();
        HistoryDayFilter.IsExpired(normal, now).Should().BeTrue();
    }

    [Fact]
    public void Add_KeepsPinnedEntriesOnTop()
    {
        var sut = new ClipboardHistoryService();
        sut.Add(new TextClipboardEntry("alt"));
        var pin = new TextClipboardEntry("angeheftet") { IsPinned = true };
        sut.Add(pin);
        sut.Resort();
        sut.Add(new TextClipboardEntry("neu")); // nicht-gepinnt, darf nicht über den Pin

        ((TextClipboardEntry)sut.Entries[0]).Text.Should().Be("angeheftet");
    }

    [Fact]
    public void MaxEntries_DoesNotDropPinned()
    {
        var sut = new ClipboardHistoryService { MaxEntries = 2 };
        var pin = new TextClipboardEntry("pin") { IsPinned = true };
        sut.Add(pin);
        sut.Resort();
        for (var i = 0; i < 5; i++) sut.Add(new TextClipboardEntry($"n{i}"));

        sut.Entries.OfType<TextClipboardEntry>().Select(t => t.Text).Should().Contain("pin");
        sut.Entries.Count(e => !e.IsPinned).Should().Be(2); // Limit gilt nur für nicht-gepinnte
    }

    [Fact]
    public void Pin_SurvivesStorageRoundtrip()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "kb-pin-" + Guid.NewGuid());
        try
        {
            var storage = new HistoryStorageService(dir);
            storage.SaveIndex([new TextClipboardEntry("merk-dir-das") { IsPinned = true }]);
            var loaded = new HistoryStorageService(dir).Load();
            loaded.Should().ContainSingle().Which.IsPinned.Should().BeTrue();
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }
}
