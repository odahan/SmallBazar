using SmallBazar.Utils;
using Microsoft.Maui.Controls;

namespace SmallBazar.Views;

public partial class DetailPage : ContentPage
{
	public DetailPage()
	{
		InitializeComponent();
		
        BindingContext = ServiceHelper.GetService<GlobalViewModel>();
    }

    private async void OnFrameTapped(object sender, EventArgs e)
    {
        var frame = sender as Frame;
        if (frame != null)
        {
            await frame.ScaleTo(0.8, 60, Easing.CubicInOut);
            await frame.ScaleTo(1, 60, Easing.CubicInOut);
        }
    }

    protected override bool OnBackButtonPressed()
    {
        // Your custom logic here
        // Return true if you handled the back button press, false otherwise
        return true;
    }
}