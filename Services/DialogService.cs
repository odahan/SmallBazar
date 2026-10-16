#nullable disable
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;

namespace SmallBazar.Services;

public class DialogService(IAppLogger appLogger) : IDialogService
{
    private Window currentWindow;

    /// <summary>
    /// Associates dialogs with the window that most recently became active.
    /// </summary>
    public void SetWindow(Window window) => currentWindow = window;

    private Page GetCurrentPage()
    {
        var page = currentWindow?.Page ?? Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page == null)
        {
            _ = appLogger.LogErrorAsync(AppLogger.LogLevel.Error, "No window is available for the dialog.");
            throw new InvalidOperationException("No window is available for the dialog.");
        }

        return page.Navigation.ModalStack.LastOrDefault() ?? page;
    }

    public Task ShowMessage(string title, string message, string buttonOk) =>
        MainThread.InvokeOnMainThreadAsync(() => GetCurrentPage().DisplayAlertAsync(title, message, buttonOk));

    public Task ShowToast(string message, ToastDuration duration) =>
        MainThread.InvokeOnMainThreadAsync(() => Toast.Make(message, duration).Show(CancellationToken.None));

    public Task<string> ShowMessageWithResponse(string title, string message, string buttonOk, string buttonCancel) =>
        MainThread.InvokeOnMainThreadAsync(() => GetCurrentPage().DisplayPromptAsync(title, message, buttonOk, buttonCancel));

    public Task<bool> ShowMessageWithBooleanResponse(string title, string message, string buttonOk, string buttonCancel) =>
        MainThread.InvokeOnMainThreadAsync(() => GetCurrentPage().DisplayAlertAsync(title, message, buttonOk, buttonCancel));

    public Task<string> InputTextDialogAsync(string title, string message, string placeholder, string initialvalue = "") =>
        MainThread.InvokeOnMainThreadAsync(() => GetCurrentPage().DisplayPromptAsync(
            title, message, "OK", "Cancel", placeholder, -1, Keyboard.Text, initialvalue));
}
