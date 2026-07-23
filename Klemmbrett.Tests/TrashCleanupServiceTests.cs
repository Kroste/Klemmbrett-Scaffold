using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Klemmbrett.Services;
using Xunit;
using Xunit.v3;

namespace Klemmbrett.Tests;

public class TrashCleanupServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "kb-trash-" + Guid.NewGuid());

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    [Fact]
    public async Task RunAsync_DeletesOnlyFilesOlderThanMinAge()
    {
        var storage = new HistoryStorageService(_dir, new TestProtector());
        var trash = storage.TrashDirectory;

        var oldFile = Path.Combine(trash, "old.png");
        var freshFile = Path.Combine(trash, "fresh.png");
        File.WriteAllBytes(oldFile, [1]);
        File.WriteAllBytes(freshFile, [1]);
        File.SetLastWriteTimeUtc(oldFile, DateTime.UtcNow.AddHours(-2));

        var sut = new TrashCleanupService(storage)
        {
            MinAge = TimeSpan.FromMinutes(10),
            Throttle = TimeSpan.Zero
        };
        await sut.RunAsync(TestContext.Current.CancellationToken);

        File.Exists(oldFile).Should().BeFalse("alte Reste sollen weg");
        File.Exists(freshFile).Should().BeTrue("frische Datei darf noch nicht gelöscht werden");
    }

    [Fact]
    public async Task RunAsync_DoesNothing_WhenTrashEmpty()
    {
        var storage = new HistoryStorageService(_dir, new TestProtector());
        var sut = new TrashCleanupService(storage) { Throttle = TimeSpan.Zero };

        var act = async () => await sut.RunAsync(TestContext.Current.CancellationToken);
        await act.Should().NotThrowAsync();
        Directory.EnumerateFiles(storage.TrashDirectory).Should().BeEmpty();
    }

    [Fact]
    public async Task ClearHistoryScenario_DoesNotDeleteImmediately()
    {
        // Regression: „Historie leeren" darf keinen Delete-Storm auslösen.
        // Verwaiste Bilder landen im Trash, TrashCleanup mit Default-MinAge lässt sie liegen.
        var storage = new HistoryStorageService(_dir, new TestProtector());
        var imagesDir = Path.Combine(_dir, "images");
        for (var i = 0; i < 5; i++)
            File.WriteAllBytes(Path.Combine(imagesDir, $"IMG{i}.png"), [1]);

        storage.SaveIndex([]); // ← genau die Historie-Leeren-Aktion

        Directory.EnumerateFiles(imagesDir).Should().BeEmpty();
        Directory.EnumerateFiles(storage.TrashDirectory).Should().HaveCount(5);

        var sut = new TrashCleanupService(storage)
        {
            MinAge = TimeSpan.FromMinutes(10),
            Throttle = TimeSpan.Zero
        };
        await sut.RunAsync(TestContext.Current.CancellationToken);
        Directory.EnumerateFiles(storage.TrashDirectory).Should().HaveCount(5,
            "frisch verschobene Reste dürfen nicht sofort gelöscht werden");
    }
}
