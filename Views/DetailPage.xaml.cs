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

    private async void OnBorderTapped(object sender, EventArgs e)
    {
        var border = sender as Border;
        if (border != null)
        {
            await border.ScaleToAsync(0.8, 60, Easing.CubicInOut);
            await border.ScaleToAsync(1, 60, Easing.CubicInOut);
        }
    }

    protected override bool OnBackButtonPressed()
    {
        // Your custom logic here
        // Return true if you handled the back button press, false otherwise
        return true;
    }
}