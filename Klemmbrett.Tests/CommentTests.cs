using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Klemmbrett.Models;
using Klemmbrett.Services;
using Xunit;

namespace Klemmbrett.Tests;

public class CommentTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "kb-comment-" + Guid.NewGuid());

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    [Fact]
    public void Comment_SurvivesStorageRoundtrip()
    {
        var storage = new HistoryStorageService(_dir);
        storage.SaveIndex([
            new TextClipboardEntry("mit-notiz") { Comment = "Zugang für Kunde X" }
        ]);

        var loaded = new HistoryStorageService(_dir).Load();

        var entry = loaded.OfType<TextClipboardEntry>().Single();
        entry.Comment.Should().Be("Zugang für Kunde X");
        entry.HasComment.Should().BeTrue();
    }

    [Fact]
    public void Entry_WithoutComment_HasNoComment()
    {
        var entry = new TextClipboardEntry("ohne");
        entry.HasComment.Should().BeFalse();
        entry.Comment.Should().BeNull();
    }

    [Fact]
    public void OldIndex_WithoutCommentField_LoadsWithNullComment()
    {
        // Simuliert eine vor der Kommentar-Funktion geschriebene history.json
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "history.json"),
            """[{"Type":"text","CapturedAt":"2026-07-23T10:00:00+00:00","Text":"alt","Hash":null,"Pinned":false}]""");

        var loaded = new HistoryStorageService(_dir).Load();

        loaded.OfType<TextClipboardEntry>().Single().Comment.Should().BeNull();
    }
}
