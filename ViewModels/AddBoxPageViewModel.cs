using Microsoft.Extensions.Logging;
using SmallBazar.Models;
using SmallBazar.Services;

namespace SmallBazar.ViewModels;

public partial class AddBoxPageViewModel : BaseViewModel
{
    private readonly IDataService dataService;
    private readonly IDialogService dialogService;
    private IPageNavigation navigation;
    private IAppLogger Logger;
    public TaskCompletionSource TaskCompletionSource { get; set; }

    // constructor 
    public AddBoxPageViewModel(IDataService dataService, IDialogService dialogService, IPageNavigation nav, IAppLogger log)
    {
        this.dataService = dataService;
        this.dialogService = dialogService;
        Logger = log;
        navigation = nav;
        //Task.Run(async ()=> await InitData());
        TaskCompletionSource = new TaskCompletionSource();
    }

    [ObservableProperty]
    private int firstMissingBoxNumber;

    [ObservableProperty]
    private int greatestUsedBoxNumber;

    [ObservableProperty]
    private int chosenBoxNumber;

    [ObservableProperty]
    private bool firstMissingIsOk;

    [ObservableProperty]
    private bool isBusy;

    partial void OnIsBusyChanged(bool value)
    {
        try
        {
            UseFirstMissingBoxNumberCommand.NotifyCanExecuteChanged();
            UseGreatestUsedBoxNumberCommand.NotifyCanExecuteChanged();
            CancelCommand.NotifyCanExecuteChanged();
            ValidateNewBoxCommand.NotifyCanExecuteChanged();
        }
        catch (Exception ex)
        {
            Logger.LogErrorAsync(AppLogger.LogLevel.Error, ex.Message);
            dialogService.ShowMessage("Error", ex.Message, "Ok");
        }
    }

    public async Task InitData()
    {
        try
        {
            FirstMissingBoxNumber = await dataService.GetFirstMissingBoxNumbersAsync();
            GreatestUsedBoxNumber = await dataService.GetLastBoxNumberAsync();
            FirstMissingIsOk = FirstMissingBoxNumber > 0;
            ChosenBoxNumber = GreatestUsedBoxNumber + 1;
        }
        catch (Exception ex)
        {
            await Logger.LogErrorAsync(AppLogger.LogLevel.Error, ex.Message);
            await dialogService.ShowMessage("Error", ex.Message, "Ok");
        }
    }

    [RelayCommand]
    private async Task ReInitData()
    {
        await InitData();
    }

    [RelayCommand(CanExecute = nameof(CanExecuteCommands))]
    public void UseFirstMissingBoxNumber()
    {
        ChosenBoxNumber = FirstMissingBoxNumber;
    }

    [RelayCommand(CanExecute = nameof(CanExecuteCommands))]
    public void UseGreatestUsedBoxNumber()
    {
        ChosenBoxNumber = GreatestUsedBoxNumber + 1;
    }

    [ObservableProperty]
    private bool hasBeenCancelled;

    [ObservableProperty]
    private int newBoxId;

    [RelayCommand(CanExecute = nameof(CanExecuteCommands))]
    public async Task Cancel()
    {
        HasBeenCancelled = true;
        NewBoxId = -1;
        await close();
    }

    [RelayCommand(CanExecute = nameof(CanExecuteCommands))]
    public async Task ValidateNewBox()
    {
        try
        {
            var b = new Box
            {
                Number = ChosenBoxNumber
            };
            var boxId = await dataService.SaveBoxAsync(b);
            if (boxId < 1)
            {
                await dialogService.ShowMessage("Error", "Box number already exists or can't be created.", "Ok");
                return;
            }
            HasBeenCancelled = false;
            NewBoxId = boxId;
            await close();
        }
        catch (Exception ex)
        {
            await Logger.LogErrorAsync(AppLogger.LogLevel.Critical, $"Error navigating to BoxDetail {ChosenBoxNumber} " + ex.Message);
            await dialogService.ShowMessage("Error", ex.Message, "Ok");
        }
    }

    private bool CanExecuteCommands()
    {
        return !IsBusy;
    }

    private async Task close()
    {
        _ = await navigation.NativeNavigation.PopModalAsync();
        TaskCompletionSource.SetResult();
    }

}
