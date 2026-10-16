using Microsoft.Windows.Storage.Pickers;

namespace SmallBazar.Services;

/// <summary>
/// Selects database paths with desktop pickers instead of marshaling WinRT storage objects.
/// </summary>
internal static class WindowsDatabasePicker
{
    /// <summary>
    /// Selects the export folder on the UI thread; cancellation returns null.
    /// </summary>
    internal static async Task<string> PickBackupFolderAsync()
    {
        var picker = new FolderPicker(GetWindow().AppWindow.Id)
        {
            CommitButtonText = "Select Folder"
        };
        var result = await picker.PickSingleFolderAsync();
        return result?.Path;
    }

    /// <summary>
    /// Opens a selected backup as a stream; cancellation returns null.
    /// </summary>
    internal static async Task<Stream> PickRestoreStreamAsync()
    {
        var picker = new FileOpenPicker(GetWindow().AppWindow.Id);
        picker.FileTypeFilter.Add("*");
        var result = await picker.PickSingleFileAsync();
        return result == null ? null : File.OpenRead(result.Path);
    }

    private static Microsoft.UI.Xaml.Window GetWindow() =>
        Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView as Microsoft.UI.Xaml.Window
        ?? throw new InvalidOperationException("No Windows window is available for the database picker.");
}
