using SmallBazar.Utils;
using Microsoft.Maui.Controls;

namespace SmallBazar.Views;

public partial class SearchPage : ContentPage
{
	public SearchPage()
	{
		InitializeComponent();
        BindingContext = ServiceHelper.GetService<GlobalViewModel>();
    }


    private void Picker_SelectedIndexChanged(object sender, EventArgs e)
    {
        searchBar.Text = "";
    }

    protected override bool OnBackButtonPressed()
    {
        // Your custom logic here
        // Return true if you handled the back button press, false otherwise
        return true;
    }
}