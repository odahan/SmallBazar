using Android.App;
using Android.Content.PM;
using Android.OS;

namespace SmallBazar
{
  //  [Activity(Theme = "@style/Theme.AppCompat.Light", // Force light theme
  //      MainLauncher = true,
  //      ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize)]

    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, 
        ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize)]
    public class MainActivity : MauiAppCompatActivity
    {
        
    }
}
