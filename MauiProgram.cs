using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.PlatformConfiguration.WindowsSpecific;
using Microsoft.Maui.Hosting;
using Microsoft.Extensions.Logging;
using SmallBazar.Services;
using SmallBazar.Utils;

namespace SmallBazar
{
    public static partial class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder.UseMauiApp<App>()
                   .UseMauiCommunityToolkit()
                   .ConfigureFonts(fonts =>
                   {
                       fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                       fonts.AddFont("OpenSans-SemiBold.ttf", "OpenSansSemiBold");
                   });

            // Services
            builder.Services.AddSingleton<IAppLogger, AppLogger>();
            builder.Services.AddSingleton<IDialogService, DialogService>();
            builder.Services.AddSingleton<IVersionService, VersionService>();
            builder.Services.AddSingleton<IDataService, DataService>();

            // Unhendled exception handler
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                var log = ServiceHelper.GetService<IAppLogger>();
                log?.LogErrorAsync(AppLogger.LogLevel.Error, "Unhandled Exception!\n"+args.ExceptionObject.ToString()).Wait();
            };

            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                var log = ServiceHelper.GetService<IAppLogger>();
                log?.LogErrorAsync(AppLogger.LogLevel.Error, "Unhandled Task Exception!\n" + args.Exception.ToString()).Wait();
            };

            // Register MainPage as a singleton and use the same instance for IPageNavigation
            builder.Services.AddSingleton<MainPage>();
            builder.Services.AddSingleton<IPageNavigation>(provider => provider.GetRequiredService<MainPage>());

            // Views and ViewModels
            builder.Services.AddSingleton<MainViewModel>(); // mostly no code. just for tabbedpage itself
            builder.Services.AddSingleton<GlobalViewModel>(); // contains all code for BoxListPage, DetailPage, SearchPage, 
            //builder.Services.AddSingleton<BoxListPage>(); -- not registered because 1 instance is created by the singleton MainMPage (created using DI)
            //builder.Services.AddSingleton<DetailPage>(); -- same as above
            //builder.Services.AddSingleton<SearchPage>(); -- same as above

#if DEBUG
            builder.Logging.AddDebug();
#endif

            var app = builder.Build();

            ServiceHelper.Initialize(app.Services);

            return app;
        }
    }

    

}
