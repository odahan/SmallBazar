using System.IO.Compression;
using Microsoft.Maui.Controls;
using SmallBazar.Services;

namespace SmallBazar.ViewModels
{
    public partial class LogPageViewModel : BaseViewModel
    {
        private readonly IAppLogger logger;
        private readonly IPageNavigation navigation;
        public LogPageViewModel(IDialogService dialog, IAppLogger log, IPageNavigation nav)
        {
            dialogService = dialog;
            navigation = nav;
            logger = log;
            TaskCompletionSource = new TaskCompletionSource();
            _ = GetLog();
        }

        public TaskCompletionSource TaskCompletionSource { get; set; }

        private readonly IDialogService dialogService;

        // Get the log content
        [ObservableProperty]
        private string logs;

        [RelayCommand]
        private async Task GetLog()
        {
            try
            {
                Logs = await logger.GetCurrentLogContentAsync();
            }
            catch (Exception ex)
            {
                await logger.LogErrorAsync(AppLogger.LogLevel.Error, "Error getting log content: " + ex.Message);
                await dialogService.ShowMessage("Error", "Error getting log content: " + ex.Message, "Ok");
            }
        }

        // Delete all logs
        [RelayCommand]
        private async Task DeleteLogs()
        {
            var res = await dialogService.ShowMessageWithBooleanResponse("Delete ALL Logs", "Are you sure you want to delete all logs?", "Yes", "No");
            if (res)
            {
                try
                {
                    await logger.DeleteAllLogs();
                    Logs = string.Empty;
                }
                catch (Exception ex)
                {
                    await logger.LogErrorAsync(AppLogger.LogLevel.Error, "Error deleting logs: " + ex.Message);
                    await dialogService.ShowMessage("Error", "Error deleting logs: " + ex.Message, "Ok");
                }
            }
        }

        // Send log by mail
        [RelayCommand]
        private async Task SendLogByMail()
        {
            if (!Email.Default.IsComposeSupported)
            {
                await logger.LogErrorAsync(AppLogger.LogLevel.Error, "Email is not supported on this device");
                await dialogService.ShowMessage("Error", "Email is not supported on this device", "Ok");
                return;
            }

            try
            {
                // Make sure that all database operations are completed before continuing.

                var emailMessage = new EmailMessage
                {
                    BodyFormat = EmailBodyFormat.PlainText,
                    Subject = "Backup BigBazar Log File",
                    Body = "Backup BigBazar Log performed on " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    + "\n" + Logs
                };
                emailMessage.To?.Add("odahan@gmail.com");

                // Attach the database file to the email.
                // Note: Make sure the file can be read and is not locked by any ongoing operation.
                //emailMessage.Attachments?.Add(new EmailAttachment(dbPath));

                // Use Email.ComposeAsync to prepare the email.
                await Email.Default.ComposeAsync(emailMessage);
                return;
            }
            catch (Exception ex)
            {
                await logger.LogErrorAsync(AppLogger.LogLevel.Error, "Error sending log backup: " + ex.Message);
                await dialogService.ShowMessage("Error", "Error sending log backup: " + ex.Message, "Ok");
                return;
            }

        }

        [RelayCommand]
        private async Task Close()
        {
            _ = await navigation.NativeNavigation.PopModalAsync();
            TaskCompletionSource.SetResult();
        }

        [RelayCommand]
        private async Task ShareLogFiles()
        {
            try
            {
                // Path to the log files directory
                var logFilesDirectory = logger.LogDirectoryPath;

                // Path to the zip file with current date
                var zipFileName = "LogsBackup_" + DateTime.Now.ToString("yyyyMMdd") + ".zip";
                var zipFilePath = Path.Combine(FileSystem.CacheDirectory, zipFileName);

                // Create the zip file
                if (File.Exists(zipFilePath))
                {
                    File.Delete(zipFilePath);
                }
                ZipFile.CreateFromDirectory(logFilesDirectory, zipFilePath);

                // Share the zip file
                await Share.RequestAsync(new ShareFileRequest
                {
                    Title = "Share Log Files",
                    File = new ShareFile(zipFilePath)
                });
            }
            catch (Exception ex)
            {
                await logger.LogErrorAsync(AppLogger.LogLevel.Error, "Error sharing log files: " + ex.Message);
                await dialogService.ShowMessage("Error", "Error sharing log files: " + ex.Message, "Ok");
            }
        }
    }
}
