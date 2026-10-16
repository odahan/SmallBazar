using System.Reflection;
using SmallBazar.Services;
using SmallBazar.Utils;
using Microsoft.Maui.Controls;

namespace SmallBazar.Views
{
    public partial class MainPage : TabbedPage, IPageNavigation
    {
        public MainPage(MainViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
            NativeNavigation = Navigation;
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, EventArgs args)
        {
            try
            {
                await ServiceHelper.GetService<GlobalViewModel>().InitializeAsync();
            }
            catch (Exception ex)
            {
                await ServiceHelper.GetService<IAppLogger>().LogErrorAsync(
                    AppLogger.LogLevel.Error, "Startup failed: " + ex);
                await DisplayAlertAsync("Startup error",
                    "The database could not be loaded. Please restart the app to retry. Your data has been preserved.", "OK");
            }
        }

        /// <summary>
        /// Very basic navigation to a page
        /// </summary>
        /// <param name="pageName">P1 to P4</param>
        /// <returns>Task</returns>
        public Task NavigateToPageAsync(string pageName = "P1")
        {
            if (string.IsNullOrEmpty(pageName)) return Task.CompletedTask;
            if (pageName == "P1") CurrentPage = Children[0];
            if (pageName == "P2") CurrentPage = Children[1];
            if (pageName == "P3") CurrentPage = Children[2];
            if (pageName == "P4") CurrentPage = Children[3];
            return Task.CompletedTask;
        }


        public INavigation NativeNavigation { get; }

        protected override bool OnBackButtonPressed()
        {
            return true;
        }
    }
}
