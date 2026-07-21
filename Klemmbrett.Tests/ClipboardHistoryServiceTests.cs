using FluentAssertions;
using Klemmbrett.Services;
using Xunit;

namespace Klemmbrett.Tests;

public class ClipboardHistoryServiceTests
{
    [Fact]
    public void Add_PutsNewestEntryFirst()
    {
        var sut = new ClipboardHistoryService();
        sut.Add("erster");
        sut.Add("zweiter");
        sut.Entries.Should().ContainInOrder("zweiter", "erster");
    }

    [Fact]
    public void Add_MovesDuplicateToFront_InsteadOfDuplicating()
    {
        var sut = new ClipboardHistoryService();
        sut.Add("a");
        sut.Add("b");
        sut.Add("a");
        sut.Entries.Should().HaveCount(2).And.ContainInOrder("a", "b");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Add_IgnoresEmptyContent(string? input)
    {
        var sut = new ClipboardHistoryService();
        sut.Add(input);
        sut.Entries.Should().BeEmpty();
    }

    [Fact]
    public void Add_RespectsMaxEntries()
    {
        var sut = new ClipboardHistoryService { MaxEntries = 3 };
        for (var i = 0; i < 5; i++) sut.Add($"eintrag-{i}");
        sut.Entries.Should().HaveCount(3).And.ContainInOrder("eintrag-4", "eintrag-3", "eintrag-2");
    }
}
