using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;

namespace SmallBazar.Services.ChatGpt;

/// <summary>Represents locally rendered box data; descriptions are sent only by an explicit analysis trigger.</summary>
public sealed record ReportBox(int Number, string Description, string Area);
public sealed record ClassificationSnapshot(string Classification, DateTimeOffset AnalyzedAt);

public interface ICategoryClassifier
{
    Task<string> ClassifyAsync(IReadOnlyList<ReportBox> boxes, CancellationToken cancellationToken);
}

/// <summary>Validates the compact historical box-to-categories format without changing its meaning.</summary>
public static class CategoryClassification
{
    private const string Styles = """
            <style>
            html{color-scheme:light dark;background:#fff}
            body{font-family:Arial,sans-serif;color:#222;background:#fff}
            table{width:100%;border-collapse:collapse;margin-bottom:20px}
            th,td{border:1px solid #777;padding:8px;text-align:left}
            th{background:#eee}
            a{color:#0065cf}
            input[type=search]{font:600 1rem Arial,sans-serif;color:#111;background:#fff;border:2px solid #666;border-radius:4px;padding:8px;min-height:44px;max-width:100%;box-sizing:border-box;caret-color:#111;color-scheme:light}
            input[type=search]:focus{outline:2px solid #005fcc;outline-offset:2px}
            @media(prefers-color-scheme:dark){
                html{background:#171717}
                body{color:#eee;background:#171717}
                th{background:#333}
                a{color:#8bc9ff}
                input[type=search]{color:#fff;background:#242424;border-color:#bdbdbd;caret-color:#fff;color-scheme:dark}
                input[type=search]:focus{outline-color:#8bc9ff}
            }
            </style>
            """;

    /// <summary>Renders the empty state with the same light and dark palette as a saved report.</summary>
    public static string RenderEmpty() =>
        "<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"UTF-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>No Report</title>" +
        Styles + "</head><body><h1>No Report</h1><p>Choose Refresh to create a report or retry an analysis.</p></body></html>";

    public static Dictionary<int, string[]> Parse(string text, IEnumerable<int> expectedNumbers = null)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new ChatGptException("The saved classification is empty or unreadable. It has not been overwritten.");
        var result = new Dictionary<int, string[]>();
        foreach (var line in text.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('|');
            if (parts.Length != 2 || !int.TryParse(parts[0].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var number))
                throw new ChatGptException("The classification format is invalid. The previous report was preserved.");
            var categories = parts[1].Split(';', StringSplitOptions.TrimEntries);
            if (categories.Any(string.IsNullOrWhiteSpace) || !result.TryAdd(number, categories.Distinct(StringComparer.OrdinalIgnoreCase).ToArray()))
                throw new ChatGptException("The classification contains empty categories or repeated box numbers. The previous report was preserved.");
        }
        if (expectedNumbers != null && (!result.Keys.ToHashSet().SetEquals(expectedNumbers) ||
            result.Values.SelectMany(value => value).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 50))
            throw new ChatGptException("The classification does not cover exactly the requested boxes within the category limit. The previous report was preserved.");
        return result;
    }

    /// <summary>Builds deterministic local HTML using current box descriptions and the saved analysis date.</summary>
    public static string Render(ClassificationSnapshot snapshot, IReadOnlyList<ReportBox> boxes)
    {
        var classified = Parse(snapshot.Classification);
        var categories = classified.Values.SelectMany(value => value).Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList();
        var current = boxes.ToDictionary(box => box.Number);
        static string Encode(string value) => WebUtility.HtmlEncode(value ?? "");
        var html = new StringBuilder("<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"UTF-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>Categorized Box Report</title>");
        html.Append(Styles);
        html.Append("<script>function scrollToAnchor(a){var e=document.getElementById(a);if(e)e.scrollIntoView({behavior:'smooth'});}function filterReport(q){q=q.toLocaleLowerCase();document.querySelectorAll('tbody tr').forEach(r=>r.hidden=!r.textContent.toLocaleLowerCase().includes(q));}</script></head><body><a id=\"top\"></a>");
        html.Append($"<h1>Categorized Box Report</h1><h3>Data extracted on {snapshot.AnalyzedAt.LocalDateTime.ToString("MMMM dd, yyyy HH:mm:ss", CultureInfo.InvariantCulture)}</h3>");
        html.Append("<label>Search <input type=\"search\" oninput=\"filterReport(this.value)\"></label><h2>Category Index</h2>");
        html.Append($"<h3>{categories.Count} entries</h3><ul>");
        for (var i = 0; i < categories.Count; i++) html.Append($"<li><a href=\"javascript:scrollToAnchor('CAT{i}')\">{Encode(categories[i])}</a></li>");
        html.Append("</ul>");
        for (var i = 0; i < categories.Count; i++)
        {
            var category = categories[i];
            html.Append($"<h2 id=\"CAT{i}\">{Encode(category)}</h2><table><thead><tr><th>Box</th><th>Description</th><th>Area</th></tr></thead><tbody>");
            foreach (var entry in classified.Where(pair => pair.Value.Contains(category, StringComparer.OrdinalIgnoreCase)).OrderBy(pair => pair.Key))
            {
                current.TryGetValue(entry.Key, out var box);
                html.Append($"<tr><td>{entry.Key}</td><td>{Encode(box?.Description ?? "Box no longer available")}</td><td>{Encode(box?.Area ?? "Unknown")}</td></tr>");
            }
            html.Append("</tbody></table><a href=\"javascript:scrollToAnchor('top')\">↑ Back to index</a>");
        }
        return html.Append("</body></html>").ToString();
    }
}

/// <summary>Keeps classification and analysis time in one atomically replaced file; legacy input is read without mutation.</summary>
public sealed class CategoryReportStore(string directory, string legacyPath)
{
    private string PathName => Path.Combine(directory, "category-report-v1.json");

    public async Task<ClassificationSnapshot> ReadAsync(CancellationToken cancellationToken)
    {
        if (File.Exists(PathName))
        {
            try
            {
                var json = JsonNode.Parse(await File.ReadAllTextAsync(PathName, cancellationToken));
                if ((int?)json["version"] != 1) throw new FormatException();
                var result = new ClassificationSnapshot((string)json["classification"],
                    DateTimeOffset.Parse((string)json["analyzed_at"], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
                CategoryClassification.Parse(result.Classification);
                return result;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception) { throw new ChatGptException("The saved classification or its date is unreadable. It has not been overwritten."); }
        }
        if (!File.Exists(legacyPath)) return null;
        var legacy = await File.ReadAllTextAsync(legacyPath, cancellationToken);
        CategoryClassification.Parse(legacy);
        return new ClassificationSnapshot(legacy, new DateTimeOffset(File.GetLastWriteTimeUtc(legacyPath)));
    }

    public async Task SaveAsync(ClassificationSnapshot snapshot, CancellationToken cancellationToken)
    {
        CategoryClassification.Parse(snapshot.Classification);
        Directory.CreateDirectory(directory);
        var temporary = PathName + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var json = new JsonObject { ["version"] = 1, ["classification"] = snapshot.Classification, ["analyzed_at"] = snapshot.AnalyzedAt.ToString("O", CultureInfo.InvariantCulture) };
            await File.WriteAllTextAsync(temporary, json.ToJsonString(), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, PathName, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}

/// <summary>Serializes explicit report triggers and publishes only fully validated and saved classifications.</summary>
public sealed class CategoryReportCoordinator(CategoryReportStore store, ICategoryClassifier classifier)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    public ClassificationSnapshot Current { get; private set; }

    public async Task<ClassificationSnapshot> OpenAsync(IReadOnlyList<ReportBox> boxes, CancellationToken cancellationToken)
    {
        if (!await gate.WaitAsync(0, cancellationToken)) return Current;
        try
        {
            Current ??= await store.ReadAsync(cancellationToken);
            return Current ?? await AnalyzeAsync(boxes, cancellationToken);
        }
        finally { gate.Release(); }
    }

    public async Task<ClassificationSnapshot> RefreshAsync(IReadOnlyList<ReportBox> boxes, CancellationToken cancellationToken)
    {
        if (!await gate.WaitAsync(0, cancellationToken)) return Current;
        try
        {
            Current ??= await store.ReadAsync(cancellationToken);
            return await AnalyzeAsync(boxes, cancellationToken);
        }
        finally { gate.Release(); }
    }

    private async Task<ClassificationSnapshot> AnalyzeAsync(IReadOnlyList<ReportBox> boxes, CancellationToken cancellationToken)
    {
        if (boxes.Count == 0) throw new ChatGptException("There are no boxes to classify. Add boxes, then choose Refresh.");
        var text = await classifier.ClassifyAsync(boxes, cancellationToken);
        CategoryClassification.Parse(text, boxes.Select(box => box.Number));
        var next = new ClassificationSnapshot(text, DateTimeOffset.UtcNow);
        await store.SaveAsync(next, cancellationToken);
        Current = next;
        return next;
    }
}
