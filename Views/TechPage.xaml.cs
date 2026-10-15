using SmallBazar.Services;
using SmallBazar.Utils;
using Microsoft.Maui.Controls;

namespace SmallBazar.Views;

public partial class TechPage : ContentPage
{
	public TechPage()
	{
		InitializeComponent();
#if DEBUG
        this.Resources["IsDebug"] = true;
#else
        this.Resources["IsDebug"] = false;
#endif
        BindingContext = ServiceHelper.GetService<GlobalViewModel>();
    }

    protected override bool OnBackButtonPressed() 
    {
       
        return true;
    }
}