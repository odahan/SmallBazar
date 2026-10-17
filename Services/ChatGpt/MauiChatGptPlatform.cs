namespace SmallBazar.Services.ChatGpt;

/// <summary>Uses the platform secure store already excluded from Android transferable backups.</summary>
public sealed class MauiChatGptCredentialStore : IChatGptCredentialStore
{
    private const string StorageKey = "smallbazar.chatgpt.prototype.v1";
    public Task<string> ReadAsync() => SecureStorage.Default.GetAsync(StorageKey);
    public Task WriteAsync(string value) => SecureStorage.Default.SetAsync(StorageKey, value);
}

/// <summary>Keeps authentication outside embedded WebViews and marshals browser launch to the UI thread.</summary>
public sealed class MauiChatGptBrowser : IChatGptBrowser
{
    public Task OpenAsync(string url) => MainThread.InvokeOnMainThreadAsync(async () =>
    {
        if (!await Browser.Default.OpenAsync(url, BrowserLaunchMode.External))
            throw new ChatGptException("The system browser could not be opened. Check the default browser and try again.");
    });
}

/// <summary>Records safe sign-in stages locally without persisting protocol payloads or credentials.</summary>
public static class MauiChatGptDiagnostics
{
    public static void Record(string message)
    {
#if ANDROID
        global::Android.Util.Log.Info("SmallBazar.ChatGpt", message);
#endif
        try
        {
            var path = Path.Combine(FileSystem.CacheDirectory, "ChatGptConnection.log");
            if (File.Exists(path) && new FileInfo(path).Length > 32768) File.WriteAllText(path, "");
            File.AppendAllText(path, $"{DateTimeOffset.UtcNow:O} {message}{Environment.NewLine}");
        }
        catch (Exception) { }
    }
}
