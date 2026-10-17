using SmallBazar.Services.ChatGpt;
using SmallBazar.ViewModels;
#if WINDOWS
using Microsoft.Web.WebView2.Core;
using Windows.Storage.Pickers;
using WinRT.Interop;
#endif

namespace SmallBazar.Views;

/// <summary>Displays locally rendered classification HTML and delegates analysis policy to the view model.</summary>
public partial class CategoryReport : ContentPage
{
    private readonly CategoryReportViewModel viewModel;
    public TaskCompletionSource TaskCompletionSource { get; } = new();

    public CategoryReport(CategoryReportViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = this.viewModel = viewModel;
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(viewModel.Html))
                ReportWebView.Source = new HtmlWebViewSource { Html = viewModel.Html };
        };
        ReportWebView.Source = new HtmlWebViewSource { Html = viewModel.Html };
        Loaded += async (_, _) =>
        {
#if WINDOWS
            await ApplyWindowsThemeAsync();
#endif
            await viewModel.OpenAsync();
        };
    }

#if WINDOWS
    protected override void OnAppearing()
    {
        base.OnAppearing();
        Application.Current.RequestedThemeChanged += OnRequestedThemeChanged;
    }

    protected override void OnDisappearing()
    {
        Application.Current.RequestedThemeChanged -= OnRequestedThemeChanged;
        base.OnDisappearing();
    }

    private void OnRequestedThemeChanged(object sender, AppThemeChangedEventArgs e) =>
        MainThread.BeginInvokeOnMainThread(async () => await ApplyWindowsThemeAsync());

    /// <summary>Synchronizes WebView2 media queries with the effective MAUI application theme.</summary>
    private async Task ApplyWindowsThemeAsync()
    {
        if (ReportWebView.Handler?.PlatformView is not Microsoft.UI.Xaml.Controls.WebView2 browser) return;
        try
        {
            await browser.EnsureCoreWebView2Async();
            if (ReportWebView.Handler?.PlatformView != browser) return;
            var dark = Application.Current.RequestedTheme == AppTheme.Dark;
            browser.RequestedTheme = dark ? Microsoft.UI.Xaml.ElementTheme.Dark : Microsoft.UI.Xaml.ElementTheme.Light;
            browser.CoreWebView2.Profile.PreferredColorScheme = dark
                ? CoreWebView2PreferredColorScheme.Dark
                : CoreWebView2PreferredColorScheme.Light;
        }
        catch (Exception ex)
        {
            MauiChatGptDiagnostics.Record($"Report browser theme could not be synchronized ({ex.GetType().Name}).");
        }
    }
#endif

    private async void OnCloseClicked(object sender, EventArgs e)
    {
        viewModel.CancelCommand.Execute(null);
        await Navigation.PopModalAsync();
        TaskCompletionSource.TrySetResult();
    }

    protected override bool OnBackButtonPressed()
    {
        OnCloseClicked(this, EventArgs.Empty);
        return true;
    }

    private async void OnExportClicked(object sender, EventArgs e)
    {
        try
        {
            var html = await viewModel.ExportAsync();
#if WINDOWS
            var picker = new FileSavePicker { SuggestedStartLocation = PickerLocationId.DocumentsLibrary, SuggestedFileName = "Report" };
            picker.FileTypeChoices.Add("HTML File", new List<string> { ".html" });
            var window = (Microsoft.UI.Xaml.Window)Application.Current.Windows[0].Handler.PlatformView;
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(window));
            var file = await picker.PickSaveFileAsync();
            if (file != null)
            {
                using var stream = await file.OpenStreamForWriteAsync();
                stream.SetLength(0);
                using var writer = new StreamWriter(stream);
                await writer.WriteAsync(html);
            }
#elif ANDROID
            var path = Path.Combine(FileSystem.CacheDirectory, "SharedReport.html");
            await File.WriteAllTextAsync(path, html);
            await Share.RequestAsync(new ShareFileRequest { Title = "Share Report", File = new ShareFile(path) });
#endif
        }
        catch (ChatGptException ex) { await DisplayAlertAsync("Report", ex.Message, "OK"); }
        catch (Exception) { await DisplayAlertAsync("Report", "The report could not be exported. The saved classification was preserved.", "OK"); }
    }
}
