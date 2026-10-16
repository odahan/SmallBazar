namespace SmallBazar
{
    public partial class App : Application
    {
        private readonly IServiceProvider services;

        public App(IServiceProvider services)
        {
            InitializeComponent();
            this.services = services;
            UserAppTheme = AppTheme.Unspecified;
        }

        protected override Window CreateWindow(IActivationState activationState)
        {
            var window = new Window(services.GetRequiredService<MainPage>())
            {
                Title = "SmallBazar",
                Width = 800,
                Height = 800
            };
            var dialogs = (SmallBazar.Services.DialogService)services.GetRequiredService<SmallBazar.Services.IDialogService>();
            dialogs.SetWindow(window);
            window.Activated += (_, _) => dialogs.SetWindow(window);
            return window;
        }
    }
}
