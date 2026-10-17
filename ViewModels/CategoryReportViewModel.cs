using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmallBazar.Services;
using SmallBazar.Services.ChatGpt;

namespace SmallBazar.ViewModels;

/// <summary>Owns report triggers and retains the last successful HTML and analysis date after failures.</summary>
public partial class CategoryReportViewModel(CategoryReportCoordinator coordinator, IDataService data) : ObservableObject
{
    private CancellationTokenSource operation;
    private bool initialized;
    private readonly ClassificationSnapshot previewSnapshot;
    private readonly IReadOnlyList<ReportBox> previewBoxes;

    /// <summary>Displays a saved synthetic result through the same report view and export path.</summary>
    internal CategoryReportViewModel(ClassificationSnapshot snapshot, IReadOnlyList<ReportBox> boxes)
        : this((CategoryReportCoordinator)null, (IDataService)null)
    {
        previewSnapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        previewBoxes = boxes ?? throw new ArgumentNullException(nameof(boxes));
        Html = CategoryClassification.Render(snapshot, boxes);
        Status = $"Saved synthetic report — last analysis: {snapshot.AnalyzedAt.LocalDateTime:g}. Viewing and export use no analysis.";
        initialized = true;
    }

    public bool IsAnalysisAvailable => previewSnapshot == null;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string Status { get; set; } = "No saved classification. Open the report to analyze your boxes.";

    [ObservableProperty]
    public partial string Html { get; set; } = CategoryClassification.RenderEmpty();

    public async Task OpenAsync()
    {
        if (initialized || IsBusy) return;
        initialized = true;
        await RunAsync(false);
    }

    [RelayCommand]
    private Task RefreshAsync() => IsAnalysisAvailable ? RunAsync(true) : Task.CompletedTask;

    [RelayCommand]
    private void Cancel() => operation?.Cancel();

    /// <summary>Rebuilds an export from the saved classification without authentication or inference.</summary>
    public async Task<string> ExportAsync()
    {
        var snapshot = previewSnapshot ?? coordinator.Current;
        if (snapshot == null) throw new ChatGptException("No saved classification is available to export.");
        return CategoryClassification.Render(snapshot, await BoxesAsync());
    }

    private async Task<List<ReportBox>> BoxesAsync()
    {
        if (previewBoxes != null) return previewBoxes.ToList();
        var boxes = await data.GetBoxesAsync();
        var areas = await data.GetAreasAsync();
        return boxes.Select(box => new ReportBox(box.Number, box.Description,
            areas.FirstOrDefault(area => area.Id == box.AreaCode)?.Name ?? "Unknown")).ToList();
    }

    private async Task RunAsync(bool refresh)
    {
        if (IsBusy) return;
        IsBusy = true;
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(15));
        operation = cancellation;
        try
        {
            Status = "Preparing the report. If analysis is needed, it uses your ChatGPT plan allowance.";
            var boxes = await BoxesAsync();
            var snapshot = refresh ? await coordinator.RefreshAsync(boxes, cancellation.Token) : await coordinator.OpenAsync(boxes, cancellation.Token);
            if (snapshot != null) Html = CategoryClassification.Render(snapshot, boxes);
            Status = snapshot == null ? "No saved classification. Choose Refresh to retry." : $"Last analysis: {snapshot.AnalyzedAt.LocalDateTime:g}";
        }
        catch (OperationCanceledException) { Status = "Analysis cancelled or timed out. The last successful report and its date were preserved. Use Refresh to retry."; }
        catch (ChatGptException ex) { Status = ex.Message; }
        catch (Exception) { Status = "The report could not be prepared. The saved classification and its date were preserved. Use Refresh to retry."; }
        finally
        {
            operation = null;
            IsBusy = false;
        }
    }
}
