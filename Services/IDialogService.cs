using CommunityToolkit.Maui.Core;

namespace SmallBazar.Services;

public interface IDialogService
{
    Task ShowMessage(string title, string message, string buttonOk);
    Task ShowToast(string message, ToastDuration duration);
    Task<string> ShowMessageWithResponse(string title, string message, string buttonOk, string buttonCancel);
    Task<bool> ShowMessageWithBooleanResponse(string title, string message, string buttonOk, string buttonCancel);
    Task<string> InputTextDialogAsync(string title, string message, string placeholder, string initialValue = "");
}