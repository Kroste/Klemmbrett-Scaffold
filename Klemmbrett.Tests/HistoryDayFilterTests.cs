using System;
using System.Linq;
using FluentAssertions;
using Klemmbrett.Models;
using Xunit;

namespace Klemmbrett.Tests;

public class HistoryDayFilterTests
{
    private static readonly DateOnly Today = new(2026, 7, 21);
    private static DateTimeOffset At(DateOnly d) => new(d, new TimeOnly(12, 0), TimeSpan.Zero);

    [Fact]
    public void BuildDayOptions_LabelsTodayAndYesterday_NewestFirst()
    {
        var entries = new IClipboardEntry[]
        {
            new TextClipboardEntry("a", At(Today)),
            new TextClipboardEntry("b", At(Today.AddDays(-1))),
            new TextClipboardEntry("c", At(Today.AddDays(-5)))
        };

        var options = HistoryDayFilter.BuildDayOptions(entries, Today);

        options.Select(o => o.Label)
            .Should().ContainInOrder("Alle (30 Tage)", "Heute", "Gestern", "16.07.2026");
    }

    [Fact]
    public void Matches_AllOption_MatchesEverything()
    {
        var all = new DayOption(null, "Alle (30 Tage)");
        HistoryDayFilter.Matches(new TextClipboardEntry("x", At(Today.AddDays(-10))), all)
            .Should().BeTrue();
    }

    [Fact]
    public void Matches_SpecificDay_FiltersByDay()
    {
        var day = new DayOption(Today.AddDays(-1), "Gestern");
        HistoryDayFilter.Matches(new TextClipboardEntry("x", At(Today.AddDays(-1))), day).Should().BeTrue();
        HistoryDayFilter.Matches(new TextClipboardEntry("y", At(Today)), day).Should().BeFalse();
    }

    [Fact]
    public void IsExpired_Beyond30Days()
    {
        var now = DateTimeOffset.Now;
        HistoryDayFilter.IsExpired(new TextClipboardEntry("alt", now.AddDays(-31)), now).Should().BeTrue();
        HistoryDayFilter.IsExpired(new TextClipboardEntry("ok", now.AddDays(-29)), now).Should().BeFalse();
    }
}
