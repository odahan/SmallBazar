namespace SmallBazar
{
    public partial class App : Application
    {
        public App(IServiceProvider services)
        {
            InitializeComponent();

            // Forcer le thème clair
            //UserAppTheme = AppTheme.Light;


            MainPage = services.GetRequiredService<MainPage>();
            UserAppTheme = PlatformAppTheme;
        }

        protected override Window CreateWindow(IActivationState activationState)
        {
            var window = base.CreateWindow(activationState);
            window.Title = "SmallBazar";
            window.Width = 800;  // Set the desired width
            window.Height = 800; // Set the desired height
            return window;
        }



    }
}
