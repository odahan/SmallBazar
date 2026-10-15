namespace SmallBazar.Views;
using Microsoft.Maui.Controls;

public partial class LogPage : ContentPage
{
	public LogPage()
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