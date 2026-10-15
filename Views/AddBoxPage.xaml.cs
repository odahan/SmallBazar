namespace SmallBazar.Views;
using Microsoft.Maui.Controls;

public partial class AddBoxPage : ContentPage
{
    public AddBoxPage()
    {
        InitializeComponent();
    }
    

    protected override void OnAppearing()
    {
        base.OnAppearing();
      //  (BindingContext as AddBoxPageViewModel)?.InitData();
    }

    protected override bool OnBackButtonPressed()
    {
        // Your custom logic here
        // Return true if you handled the back button press, false otherwise
        return true;
    }

}