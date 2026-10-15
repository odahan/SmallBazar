namespace SmallBazar.Views;
using Microsoft.Maui.Controls;

public partial class AreaPage : ContentPage
{
    public AreaPage()
    {
        InitializeComponent();
    }

    protected override bool OnBackButtonPressed()
    {
        // Your custom logic here
        // Return true if you handled the back button press, false otherwise
        return true;
    }
}