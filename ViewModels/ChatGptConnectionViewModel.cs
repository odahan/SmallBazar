using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmallBazar.Services.ChatGpt;

namespace SmallBazar.ViewModels;

/// <summary>Manages ChatGPT accounts and report models, with optional development diagnostics.</summary>
public partial class ChatGptConnectionViewModel(ChatGptSession session, ChatGptInference inference, ICategoryClassifier classifier) : ObservableObject
{
    private CancellationTokenSource operation;
    private string modelsAccount;
    private bool modelsLoaded;

    [ObservableProperty]
    public partial string Account { get; set; } = "Not connected";

    [ObservableProperty]
    public partial string Status { get; set; } = "Choose an account and report model. Opening this page does not run an analysis.";

    [ObservableProperty]
    public partial string ConnectionStatus { get; set; } = "No sign-in attempt in this session.";

    [ObservableProperty]
    public partial List<ChatGptModel> Models { get; set; } = [];

    [ObservableProperty]
    public partial ChatGptModel SelectedModel { get; set; }

    [ObservableProperty]
    public partial string SavedModelStatus { get; set; } = "No report model saved.";

    [ObservableProperty]
    public partial List<ChatGptAccount> Accounts { get; set; } = [];

    [ObservableProperty]
    public partial ChatGptAccount SelectedAccount { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    public partial bool IsBusy { get; set; }

    public bool IsIdle => !IsBusy;

    /// <summary>Shows synthetic validation controls only in development builds.</summary>
    public bool IsDiagnosticsVisible
    {
        get
        {
#if DEBUG
            return true;
#else
            return false;
#endif
        }
    }

    public async Task InitializeAsync()
    {
        try
        {
            Account = await session.AccountAsync();
            ConnectionStatus = session.LastSignInStatus;
            if (ConnectionStatus == "No sign-in attempt in this session." && Account != "Not connected")
                ConnectionStatus = "Saved account is available on this device.";
            Accounts = await session.AccountsAsync();
            var active = await session.ActiveClientIdAsync();
            SelectedAccount = Accounts.FirstOrDefault(item => item.ClientId == active);
            if (modelsAccount != active)
            {
                modelsAccount = active;
                modelsLoaded = false;
                Models = [];
            }
            var savedModel = await session.SelectedModelDetailsAsync();
            SavedModelStatus = savedModel == null ? "No report model saved." : $"Saved report model: {savedModel.DisplayName}";
            if (!modelsLoaded) Models = savedModel == null ? [] : [savedModel];
            SelectedModel = Models.FirstOrDefault(item => item.Slug == savedModel?.Slug);
            if (modelsLoaded && savedModel != null && SelectedModel == null)
                SavedModelStatus += " — not available in the loaded catalog. Select another model.";
        }
        catch (Exception) { Status = "The protected ChatGPT session could not be read. Please check this device's secure storage."; }
    }

    [RelayCommand]
    private Task SignInAsync() => ConnectAsync(false);

    [RelayCommand]
    private Task AddAccountAsync() => ConnectAsync(true);

    private Task ConnectAsync(bool addAccount) => RunAsync(async token =>
    {
        Status = "Complete sign-in and plan authorization in the system browser, then return here. You have up to 15 minutes, including switching to your authenticator app.";
        session.SignInProgress += OnSignInProgress;
        try { await session.SignInAsync(token, operation.Token, addAccount); }
        finally { session.SignInProgress -= OnSignInProgress; }
        Models = [];
        SelectedModel = null;
        modelsLoaded = false;
        Status = "Identity verified. Load models to choose or change the report model. No analysis was requested.";
    }, TimeSpan.FromMinutes(15));

    private void OnSignInProgress(string message) => ConnectionStatus = message;

    [RelayCommand]
    private Task UseAccountAsync() => RunAsync(async token =>
    {
        if (SelectedAccount == null) throw new ChatGptException("Select a saved ChatGPT account first.");
        await session.SelectAccountAsync(SelectedAccount.ClientId, token);
        Models = [];
        SelectedModel = null;
        modelsLoaded = false;
        Status = "Account selected. Its saved report model is restored when available. Load models to change it. Your saved report was preserved.";
    });

    [RelayCommand]
    private Task UseModelAsync() => RunAsync(async token =>
    {
        await session.SelectModelAsync(SelectedModel, token);
        Status = "Model saved for the active account. No analysis was requested.";
    });

    [RelayCommand]
    private Task LoadModelsAsync() => RunAsync(async token =>
    {
        Status = "Loading models available to this ChatGPT account…";
        Models = await inference.ModelsAsync(token);
        modelsLoaded = true;
        var selected = await session.SelectedModelAsync();
        SelectedModel = Models.FirstOrDefault(item => item.Slug == selected);
        if (SelectedModel != null) await session.SelectModelAsync(SelectedModel, token);
        Status = Models.Count == 0 ? "No models are available to this account." : "Choose a model and use selected model for reports to save a new selection. No analysis was requested.";
    });

    [RelayCommand]
    private Task TestAsync() => RunAsync(async token =>
    {
        Status = "Running one synthetic generation using the ChatGPT plan…";
        await session.SelectModelAsync(SelectedModel, token);
        var result = await inference.TestAsync(SelectedModel, token);
        Status = $"Synthetic generation completed: {result}. Model: {SelectedModel.DisplayName}. " +
            "No personal box descriptions were sent. The AI Category Report has not been changed.";
    });

    [RelayCommand]
    private Task ReportCheckAsync() => RunAsync(async token =>
    {
        await session.SelectModelAsync(SelectedModel, token);
        Status = "Checking the report workflow with two synthetic boxes and one analysis…";
        var boxes = SyntheticReportPreview.Boxes;
        var directory = Path.Combine(FileSystem.CacheDirectory, "SyntheticReportValidation");
        var store = new CategoryReportStore(directory, Path.Combine(directory, "unused-legacy.txt"));
        var report = new CategoryReportCoordinator(store, classifier);
        var analyzed = await report.RefreshAsync(boxes, token);
        var loaded = await new CategoryReportCoordinator(store, classifier).OpenAsync(boxes, token);
        var html = CategoryClassification.Render(loaded, boxes);
        if (analyzed != loaded || !html.Contains("Synthetic hammer") || !html.Contains("Synthetic screwdriver"))
            throw new ChatGptException("The synthetic report could not be verified after reloading.");
        await File.WriteAllTextAsync(Path.Combine(directory, "Report.html"), html, token);
        Status = $"Synthetic report workflow verified. Analysis saved at {loaded.AnalyzedAt.LocalDateTime:g}; reloaded and HTML rebuilt without another analysis. Your personal report was not changed.";
        MauiChatGptDiagnostics.Record("Synthetic report workflow verified: saved classification and timestamp reloaded; local HTML rebuilt without another analysis.");
    });

    [RelayCommand]
    private Task SignOutAsync() => RunAsync(async token =>
    {
        var confirmed = await session.SignOutAsync(token);
        Models = [];
        SelectedModel = null;
        modelsLoaded = false;
        Status = confirmed ? "Signed out. The renewable session was revoked." :
            "Signed out locally. Remote revocation was not confirmed. Disconnect SmallBazar in ChatGPT Settings → Usage.";
    });

    [RelayCommand]
    private void Cancel() => operation?.Cancel();

    [RelayCommand]
    private async Task OpenUsageAsync()
    {
        try { await Browser.Default.OpenAsync("https://chatgpt.com/#settings/Usage", BrowserLaunchMode.External); }
        catch (Exception) { Status = "Open ChatGPT Settings → Usage to review limits and disconnect this app."; }
    }

    private async Task RunAsync(Func<CancellationToken, Task> action, TimeSpan? timeout = null)
    {
        if (IsBusy) return;
        IsBusy = true;
        using var userCancellation = new CancellationTokenSource();
        using var deadline = new CancellationTokenSource(timeout ?? TimeSpan.FromMinutes(2));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(userCancellation.Token, deadline.Token);
        operation = userCancellation;
        try { await action(cancellation.Token); }
        catch (ChatGptException ex) { Status = ex.Message; }
        catch (OperationCanceledException)
        {
            Status = userCancellation.IsCancellationRequested
                ? "The operation was cancelled in SmallBazar. No generation was retried."
                : "The operation timed out. Start a new attempt in SmallBazar. No generation was retried.";
        }
        catch (HttpRequestException) { Status = "OpenAI could not be reached. Check the network and try again explicitly."; }
        catch (Exception ex)
        {
            Status = $"The operation could not be completed ({ex.GetType().Name}). No report data was changed. Try again explicitly.";
            MauiChatGptDiagnostics.Record(Status);
        }
        finally
        {
            operation = null;
            await InitializeAsync();
            IsBusy = false;
        }
    }
}
