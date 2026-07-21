using System.Text;
using FluentAssertions;
using Klemmbrett.Models;
using Xunit;

namespace Klemmbrett.Tests;

public class ClipboardEntryTests
{
    [Fact]
    public void TextEntry_Preview_IsFirstLineOnly()
    {
        var e = new TextClipboardEntry("erste Zeile\nzweite Zeile");
        e.Preview.Should().Be("erste Zeile");
        e.Text.Should().Contain("zweite Zeile"); // Volltext bleibt erhalten
    }

    [Fact]
    public void TextEntry_Preview_TruncatesLongLines()
    {
        var e = new TextClipboardEntry(new string('x', 300));
        e.Preview.Length.Should().BeLessThanOrEqualTo(121);
        e.Preview.Should().EndWith("…");
    }

    [Fact]
    public void TextEntry_DedupeKey_DependsOnFullText()
    {
        new TextClipboardEntry("a\nb").DedupeKey
            .Should().NotBe(new TextClipboardEntry("a\nc").DedupeKey);
    }

    [Fact]
    public void ImageHash_IsStableAndContentSensitive()
    {
        var a = Encoding.UTF8.GetBytes("bild-a");
        var b = Encoding.UTF8.GetBytes("bild-b");
        ImageClipboardEntry.ComputeHash(a).Should().Be(ImageClipboardEntry.ComputeHash(a));
        ImageClipboardEntry.ComputeHash(a).Should().NotBe(ImageClipboardEntry.ComputeHash(b));
    }
}
