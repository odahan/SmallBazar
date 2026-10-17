namespace SmallBazar.Services.ChatGpt;

/// <summary>Reads the saved synthetic check without authentication, inference, or personal data access.</summary>
public static class SyntheticReportPreview
{
    public static IReadOnlyList<ReportBox> Boxes { get; } = Array.AsReadOnly(new[]
    {
        new ReportBox(1, "Synthetic hammer", "Synthetic garage"),
        new ReportBox(2, "Synthetic screwdriver", "Synthetic shelf")
    });

    /// <summary>Loads an existing synthetic classification and preserves its analysis timestamp.</summary>
    public static async Task<ClassificationSnapshot> LoadAsync(string directory, CancellationToken cancellationToken)
    {
        var store = new CategoryReportStore(directory, Path.Combine(directory, "unused-legacy.txt"));
        var snapshot = await store.ReadAsync(cancellationToken);
        if (snapshot == null)
            throw new ChatGptException("No saved synthetic report is available. Viewing does not run an analysis.");
        CategoryClassification.Parse(snapshot.Classification, Boxes.Select(box => box.Number));
        return snapshot;
    }
}
