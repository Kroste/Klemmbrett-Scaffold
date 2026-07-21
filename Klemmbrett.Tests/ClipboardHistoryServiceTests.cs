using System.Linq;
using FluentAssertions;
using Klemmbrett.Models;
using Klemmbrett.Services;
using Xunit;

namespace Klemmbrett.Tests;

public class ClipboardHistoryServiceTests
{
    private static TextClipboardEntry T(string s) => new(s);

    [Fact]
    public void Add_PutsNewestEntryFirst()
    {
        var sut = new ClipboardHistoryService();
        sut.Add(T("erster"));
        sut.Add(T("zweiter"));
        sut.Entries.Select(e => ((TextClipboardEntry)e).Text)
            .Should().ContainInOrder("zweiter", "erster");
    }

    [Fact]
    public void Add_MovesDuplicateToFront_InsteadOfDuplicating()
    {
        var sut = new ClipboardHistoryService();
        sut.Add(T("a"));
        sut.Add(T("b"));
        sut.Add(T("a"));
        sut.Entries.Should().HaveCount(2);
        sut.Entries.Select(e => ((TextClipboardEntry)e).Text)
            .Should().ContainInOrder("a", "b");
    }

    [Fact]
    public void Add_IgnoresNull()
    {
        var sut = new ClipboardHistoryService();
        sut.Add(null);
        sut.Entries.Should().BeEmpty();
    }

    [Fact]
    public void Add_RespectsMaxEntries()
    {
        var sut = new ClipboardHistoryService { MaxEntries = 3 };
        for (var i = 0; i < 5; i++) sut.Add(T($"eintrag-{i}"));
        sut.Entries.Should().HaveCount(3);
        sut.Entries.Select(e => ((TextClipboardEntry)e).Text)
            .Should().ContainInOrder("eintrag-4", "eintrag-3", "eintrag-2");
    }

    [Fact]
    public void Add_DeduplicatesAcrossTypes_ByKey()
    {
        var sut = new ClipboardHistoryService();
        sut.Add(T("gleich"));
        sut.Add(T("gleich"));
        sut.Entries.Should().HaveCount(1);
    }
}
