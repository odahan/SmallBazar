using System.Text;

namespace SmallBazar.Utils;

/// <summary>
/// Charge un prompt UTF8+BOM depuis un fichier texte dans les assets de l'application. 
/// </summary>
public static class PromptLoader
{
    private static string _cachedPrompt;

    public static async Task<string> LoadAsync(string assetName = "category_prompt.txt")
    {
        if (_cachedPrompt != null) return _cachedPrompt;

        await using var stream = await FileSystem.OpenAppPackageFileAsync(assetName);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        _cachedPrompt = await reader.ReadToEndAsync();

        // Normalisation légère (évite concat sans CRLF)
        if (!_cachedPrompt.EndsWith("\n")) _cachedPrompt += Environment.NewLine;

        return _cachedPrompt;
    }
}