using SmallBazar.Services.ChatGpt;
using Xunit;

namespace SmallBazar.ChatGpt.Tests;

public sealed class SyntheticReportPreviewTests
{
    [Fact]
    public async Task PreviewPreservesTheSavedClassificationTimestampAndFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"SmallBazar-preview-{Guid.NewGuid():N}");
        try
        {
            var store = new CategoryReportStore(directory, Path.Combine(directory, "unused-legacy.txt"));
            var saved = new ClassificationSnapshot("1 | Tools\n2 | Tools", DateTimeOffset.Parse("2026-10-17T12:59:50+00:00"));
            await store.SaveAsync(saved, CancellationToken.None);
            var path = Path.Combine(directory, "category-report-v1.json");
            var before = await File.ReadAllBytesAsync(path);

            var loaded = await SyntheticReportPreview.LoadAsync(directory, CancellationToken.None);
            var html = CategoryClassification.Render(loaded, SyntheticReportPreview.Boxes);

            Assert.Equal(saved, loaded);
            Assert.Equal(before, await File.ReadAllBytesAsync(path));
            Assert.Contains("Synthetic hammer", html);
            Assert.Contains("Synthetic screwdriver", html);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task MissingSavedReportFailsWithoutCreatingData()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"SmallBazar-preview-{Guid.NewGuid():N}");
        await Assert.ThrowsAsync<ChatGptException>(() => SyntheticReportPreview.LoadAsync(directory, CancellationToken.None));
        Assert.False(Directory.Exists(directory));
    }

    [Fact]
    public async Task SavedReportWithOtherBoxNumbersIsRejectedAndPreserved()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"SmallBazar-preview-{Guid.NewGuid():N}");
        try
        {
            var store = new CategoryReportStore(directory, Path.Combine(directory, "unused-legacy.txt"));
            await store.SaveAsync(new ClassificationSnapshot("84 | Tools", DateTimeOffset.UtcNow), CancellationToken.None);
            var path = Path.Combine(directory, "category-report-v1.json");
            var before = await File.ReadAllBytesAsync(path);

            await Assert.ThrowsAsync<ChatGptException>(() => SyntheticReportPreview.LoadAsync(directory, CancellationToken.None));

            Assert.Equal(before, await File.ReadAllBytesAsync(path));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
