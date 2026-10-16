using Microsoft.Maui.Controls;
using SmallBazar.Models;
using SmallBazar.Services;
using SmallBazar.Utils;

namespace SmallBazar.Views;

public partial class BoxListPage : ContentPage
{
    public BoxListPage()
    {
        InitializeComponent();

        VisualStateManager.GoToState(FilterButton, "NotFiltered");

        BindingContext = ServiceHelper.GetService<GlobalViewModel>();

    }

    private async void OnBarButtonClicked(object sender, EventArgs e)
    {
        if (sender is not View button) return;
        await Animator.AnimateClicView(button);
    }

    private void CollectionView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is CollectionView cv && e.CurrentSelection.FirstOrDefault() is Box selectedItem)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // Vérifiez que l'élément sélectionné existe dans la collection avant de faire défiler
                if (cv.ItemsSource is IList<Box> itemsSource && itemsSource.Contains(selectedItem))
                {
                    cv.ScrollTo(selectedItem, position: ScrollToPosition.MakeVisible, animate: true);
                }
            });
        }
    }

    protected override bool OnBackButtonPressed()
    {
        // Your custom logic here
        // Return true if you handled the back button press, false otherwise
        return true;
    }



}