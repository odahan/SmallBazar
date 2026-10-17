using SmallBazar.Services.ChatGpt;
using Xunit;

namespace SmallBazar.ChatGpt.Tests;

public sealed class CategoryReportTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "SmallBazar-report-" + Guid.NewGuid().ToString("N"));
    private readonly List<ReportBox> boxes = [new(1, "Hammer <script>", "Garage"), new(2, "Pen", "Office")];

    private CategoryReportStore Store => new(directory, Path.Combine(directory, "categorizedData.txt"));

    [Fact]
    public async Task InitialOpenAnalyzesOnceAndAllLaterOpensAreLocalIncludingRestart()
    {
        var classifier = new FakeClassifier();
        var coordinator = new CategoryReportCoordinator(Store, classifier);
        var first = await coordinator.OpenAsync(boxes, default);
        for (var i = 0; i < 4; i++) Assert.Equal(first, await coordinator.OpenAsync(boxes, default));
        var restarted = new CategoryReportCoordinator(Store, new RejectClassifier());
        Assert.Equal(first, await restarted.OpenAsync(boxes, default));
        Assert.Equal(1, classifier.Calls);
    }

    [Fact]
    public async Task BoxChangesAndExportsKeepTheSavedDateWithoutAnalysis()
    {
        var classifier = new FakeClassifier();
        var coordinator = new CategoryReportCoordinator(Store, classifier);
        var first = await coordinator.OpenAsync(boxes, default);
        boxes[0] = new ReportBox(1, "New description", "New area");
        boxes.Add(new ReportBox(3, "Additional box", "Attic"));
        var loaded = await coordinator.OpenAsync(boxes, default);
        var html = CategoryClassification.Render(loaded, boxes);
        Assert.Contains("New description", html);
        Assert.DoesNotContain("Additional box", html);
        Assert.Equal(first.AnalyzedAt, loaded.AnalyzedAt);
        Assert.Equal(html, CategoryClassification.Render(loaded, boxes));
        Assert.Equal(1, classifier.Calls);
    }

    [Theory]
    [InlineData("network")]
    [InlineData("quota")]
    [InlineData("cancel")]
    [InlineData("invalid")]
    [InlineData("unknown-box")]
    [InlineData("missing-box")]
    public async Task FailedRefreshPreservesClassificationAndTimestampOnDisk(string scenario)
    {
        var classifier = new FakeClassifier();
        var coordinator = new CategoryReportCoordinator(Store, classifier);
        var first = await coordinator.OpenAsync(boxes, default);
        classifier.Scenario = scenario;
        await Assert.ThrowsAnyAsync<Exception>(() => coordinator.RefreshAsync(boxes, default));
        Assert.Equal(first, coordinator.Current);
        Assert.Equal(first, await Store.ReadAsync(default));
        Assert.Equal(2, classifier.Calls);
    }

    [Fact]
    public async Task EveryAcceptedRefreshMakesOneCallAndReplacesTheSnapshotOnlyOnSuccess()
    {
        var classifier = new FakeClassifier();
        var coordinator = new CategoryReportCoordinator(Store, classifier);
        await coordinator.OpenAsync(boxes, default);
        classifier.Text = "1 | Hardware\n2 | Stationery";
        var next = await coordinator.RefreshAsync(boxes, default);
        Assert.Equal(classifier.Text, next.Classification);
        Assert.Equal(next, await Store.ReadAsync(default));
        Assert.Equal(2, classifier.Calls);
    }

    [Fact]
    public async Task ConcurrentRefreshDoesNotQueueAnotherGeneration()
    {
        var blocker = new TaskCompletionSource<string>();
        var classifier = new FakeClassifier { Pending = blocker.Task };
        var coordinator = new CategoryReportCoordinator(Store, classifier);
        var first = coordinator.OpenAsync(boxes, default);
        await coordinator.RefreshAsync(boxes, default);
        Assert.Equal(1, classifier.Calls);
        blocker.SetResult("1 | Tools\n2 | Stationery");
        await first;
        Assert.Equal(1, classifier.Calls);
    }

    [Fact]
    public async Task LegacyClassificationKeepsItsTimestampAndFileContents()
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "categorizedData.txt");
        var legacy = "1 | Tools; Hardware\r\n2 | Stationery\r\n";
        await File.WriteAllTextAsync(path, legacy);
        var date = new DateTime(2025, 2, 3, 4, 5, 6, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(path, date);
        var result = await new CategoryReportCoordinator(Store, new RejectClassifier()).OpenAsync(boxes, default);
        Assert.Equal(legacy, result.Classification);
        Assert.Equal(date, result.AnalyzedAt.UtcDateTime);
        Assert.Equal(legacy, await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task CorruptSavedDataNeverTriggersOrGetsOverwrittenByAnalysis()
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "category-report-v1.json");
        await File.WriteAllTextAsync(path, "unreadable");
        var classifier = new FakeClassifier();
        var coordinator = new CategoryReportCoordinator(Store, classifier);
        await Assert.ThrowsAsync<ChatGptException>(() => coordinator.OpenAsync(boxes, default));
        await Assert.ThrowsAsync<ChatGptException>(() => coordinator.RefreshAsync(boxes, default));
        Assert.Equal("unreadable", await File.ReadAllTextAsync(path));
        Assert.Equal(0, classifier.Calls);
    }

    [Fact]
    public async Task CancelledAtomicSaveLeavesThePreviousFileIntact()
    {
        var previous = new ClassificationSnapshot("1 | Tools", DateTimeOffset.UtcNow.AddDays(-2));
        await Store.SaveAsync(previous, default);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Store.SaveAsync(new("1 | Other", DateTimeOffset.UtcNow), cancellation.Token));
        Assert.Equal(previous, await Store.ReadAsync(default));
        Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
    }

    [Fact]
    public void LocalHtmlEscapesDescriptionsAndUsesExactCategoryMembership()
    {
        var snapshot = new ClassificationSnapshot("1 | Tool\n2 | Tools; Stationery", DateTimeOffset.UtcNow);
        var html = CategoryClassification.Render(snapshot, boxes);
        Assert.Contains("&lt;script&gt;", html);
        Assert.DoesNotContain("Hammer <script>", html);
        var toolSection = html[html.IndexOf(">Tool</h2>", StringComparison.Ordinal)..].Split("</table>")[0];
        Assert.DoesNotContain("Pen", toolSection);
    }

    private sealed class RejectClassifier : ICategoryClassifier
    {
        public Task<string> ClassifyAsync(IReadOnlyList<ReportBox> boxes, CancellationToken cancellationToken) => throw new Exception("Offline or disconnected: no analysis permitted.");
    }

    private sealed class FakeClassifier : ICategoryClassifier
    {
        public int Calls;
        public string Scenario;
        public string Text = "1 | Tools\n2 | Stationery";
        public Task<string> Pending;
        public Task<string> ClassifyAsync(IReadOnlyList<ReportBox> boxes, CancellationToken cancellationToken)
        {
            Calls++;
            if (Scenario == "cancel") throw new OperationCanceledException();
            if (Scenario is "network" or "quota") throw new ChatGptException(Scenario);
            return Pending ?? Task.FromResult(Scenario switch
            {
                "invalid" => "not a classification", "unknown-box" => "1 | Tools\n99 | Other", "missing-box" => "1 | Tools", _ => Text
            });
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
}
