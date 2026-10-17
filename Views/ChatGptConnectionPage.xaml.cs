using SmallBazar.ViewModels;
using SmallBazar.Services.ChatGpt;

namespace SmallBazar.Views;

/// <summary>Hosts account and model settings and development diagnostics.</summary>
public partial class ChatGptConnectionPage : ContentPage
{
    private readonly ChatGptConnectionViewModel viewModel;

    public ChatGptConnectionPage(ChatGptConnectionViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = this.viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await viewModel.InitializeAsync();
    }

    protected override bool OnBackButtonPressed()
    {
        viewModel.CancelCommand.Execute(null);
        return base.OnBackButtonPressed();
    }

    private async void CloseClicked(object sender, EventArgs e)
    {
        viewModel.CancelCommand.Execute(null);
        await Navigation.PopModalAsync();
    }

    /// <summary>Opens the existing synthetic result in the production WebView and export UI.</summary>
    private async void ViewSyntheticReportClicked(object sender, EventArgs e)
    {
        if (viewModel.IsBusy) return;
        viewModel.IsBusy = true;
        try
        {
            var directory = Path.Combine(FileSystem.CacheDirectory, "SyntheticReportValidation");
            var snapshot = await SyntheticReportPreview.LoadAsync(directory, CancellationToken.None);
            await Navigation.PushModalAsync(new CategoryReport(new CategoryReportViewModel(snapshot, SyntheticReportPreview.Boxes)));
        }
        catch (ChatGptException ex) { await DisplayAlertAsync("Synthetic report", ex.Message, "OK"); }
        catch (Exception) { await DisplayAlertAsync("Synthetic report", "The saved synthetic report could not be opened. No analysis was requested.", "OK"); }
        finally { viewModel.IsBusy = false; }
    }
}
