using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.PlatformConfiguration.WindowsSpecific;
using Microsoft.Maui.Hosting;
using Microsoft.Extensions.Logging;
using SmallBazar.Services;
using SmallBazar.Utils;
using SmallBazar.Services.ChatGpt;

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
            builder.Services.AddSingleton<IChatGptCredentialStore, MauiChatGptCredentialStore>();
            builder.Services.AddSingleton<IChatGptBrowser, MauiChatGptBrowser>();
            builder.Services.AddSingleton(provider => new ChatGptSession(CreateChatGptHttpClient(),
                provider.GetRequiredService<IChatGptCredentialStore>(), provider.GetRequiredService<IChatGptBrowser>(),
                MauiChatGptDiagnostics.Record));
            builder.Services.AddSingleton(provider => new ChatGptInference(CreateChatGptHttpClient(),
                provider.GetRequiredService<ChatGptSession>(), MauiChatGptDiagnostics.Record));
            builder.Services.AddTransient<ChatGptConnectionViewModel>();
            builder.Services.AddTransient<Views.ChatGptConnectionPage>();
            builder.Services.AddSingleton<ICategoryClassifier, MauiCategoryClassifier>();
            builder.Services.AddSingleton(provider => new CategoryReportStore(Path.Combine(FileSystem.AppDataDirectory, "Reports"),
                Path.Combine(FileSystem.CacheDirectory, "categorizedData.txt")));
            builder.Services.AddSingleton<CategoryReportCoordinator>();
            builder.Services.AddTransient<CategoryReportViewModel>();
            builder.Services.AddTransient<Views.CategoryReport>();

            // Unhendled exception handler
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                var log = ServiceHelper.GetService<IAppLogger>();
                _ = log?.LogErrorAsync(AppLogger.LogLevel.Error, "Unhandled Exception!\n"+args.ExceptionObject.ToString());
            };

            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                var log = ServiceHelper.GetService<IAppLogger>();
                _ = log?.LogErrorAsync(AppLogger.LogLevel.Error, "Unhandled Task Exception!\n" + args.Exception.ToString());
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

        /// <summary>Disables redirects so OAuth credentials cannot be forwarded to another origin.</summary>
        private static HttpClient CreateChatGptHttpClient() => new(new HttpClientHandler { AllowAutoRedirect = false })
        {
            Timeout = TimeSpan.FromMinutes(2)
        };
    }

    

}
