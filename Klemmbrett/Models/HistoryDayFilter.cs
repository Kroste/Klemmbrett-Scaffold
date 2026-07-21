using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Klemmbrett.Models;

/// <summary>Eine Auswahloption des Tagesfilters. Date == null bedeutet „Alle".</summary>
public sealed record DayOption(DateOnly? Date, string Label)
{
    public override string ToString() => Label;
}

/// <summary>UI-freie Logik für den Tagesfilter (max. 30 Tage) — xUnit-testbar.</summary>
public static class HistoryDayFilter
{
    public const int RetentionDays = 30;

    /// <summary>„Alle" plus alle Tage (neueste zuerst), an denen Einträge existieren.</summary>
    public static List<DayOption> BuildDayOptions(IEnumerable<IClipboardEntry> entries, DateOnly today)
    {
        var options = new List<DayOption> { new(null, "Alle (30 Tage)") };
        var days = entries
            .Select(e => DateOnly.FromDateTime(e.CapturedAt.LocalDateTime))
            .Distinct()
            .OrderDescending();

        foreach (var day in days)
        {
            var label = day == today ? "Heute"
                : day == today.AddDays(-1) ? "Gestern"
                : day.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
            options.Add(new DayOption(day, label));
        }
        return options;
    }

    public static bool Matches(IClipboardEntry entry, DayOption? option)
        => option?.Date is not { } day
           || DateOnly.FromDateTime(entry.CapturedAt.LocalDateTime) == day;

    public static bool IsExpired(IClipboardEntry entry, DateTimeOffset now)
        => entry.CapturedAt < now.AddDays(-RetentionDays);
}
