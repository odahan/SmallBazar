using System.Collections;
using SmallBazar.Services;
using SmallBazar.Models;
using System.Globalization;
using System.Text;
using System.Collections.ObjectModel;
using SmallBazar.Utils;
using System.Reflection;
using System.Diagnostics;
using System.IO.Compression;
using static SQLite.SQLite3;

namespace SmallBazar.ViewModels
{
    public partial class GlobalViewModel : BaseViewModel
    {

        #region Ctor
        /// <summary>
        /// Global View Model for List/Detail and Search page. Singleton.
        /// </summary>
        /// <param name="data">Data Service</param>
        /// <param name="log">Logger</param>
        /// <param name="dialog">Dialog service</param>
        public GlobalViewModel(IDataService data, IAppLogger log, IDialogService dialog, IVersionService vService)
        {
            dataService = data;
            logger = log;
            dialogService = dialog;
            versionService = vService;
            Version = versionService.Version;
            SoftDescription = versionService.Description;
            Company = versionService.Company;
            Product = versionService.Product;
            Copyright = versionService.Copyright;
            Task.Run(async () =>
            {
                await initStorage();
                await LoadCategoriesAsync();
                await LoadAreasAsync();
                await LoadDataAsync();
                InitSearchModes();
                await logger.LogErrorAsync(AppLogger.LogLevel.Information, $"GlobalViewModel created Db count {BoxList?.Count}");
            });



        }

        private void InitSearchModes()
        {
            SearchModes = Enum.GetValues(typeof(BoxSearchMode));
            CurrentSearchMode = BoxSearchMode.Description;
            setPlaceHolder();
            OnCurrentSearchModeChanged(BoxSearchMode.Default, BoxSearchMode.Description);
        }

        private IAppLogger logger { get; }

        private IDialogService dialogService { get; }

        private IVersionService versionService { get; }

        [ObservableProperty]
        private bool isBusy;


        private IDataService dataService;
        #endregion

        #region List page
        private async Task LoadDataAsync()
        {
            SelectedBox = null;
            BoxList = await dataService.GetBoxesAsync();
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(BoxCount))]
        private List<Box> boxList;

        /// <summary>
        /// Number of boxes currently in the list
        /// </summary>
        public int BoxCount => BoxList?.Count ?? 0;

        [ObservableProperty]
        private Box selectedBox;


        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsSortByDescription))]
        private bool isSortByNumber = true;

        /// <summary>
        /// Is the list sorted by description (or number)
        /// </summary>
        public bool IsSortByDescription => !IsSortByNumber;


        partial void OnIsSortByNumberChanged(bool oldValue, bool newValue)
        {
            sortList().Wait();
        }

        private async Task sortList()
        {
            try
            {
                if (BoxList == null) return;

                List<Box> sortedList;
                var bl = new List<Box>(BoxList);


                sortedList = IsSortByNumber
                    ? bl.OrderBy(b => b.Number).ToList()
                    : bl.OrderBy(b => Tools.CleanString(b.Description)).ToList();


                SelectedBox = null;
                BoxList = null;
                BoxList = [.. sortedList];

            }
            catch (Exception ex)
            {
                await logger.LogErrorAsync(AppLogger.LogLevel.Error, "Error in sortList: " + ex.Message);
                return;
            }
        }



        [ObservableProperty]
        private bool isFiltered;


        [RelayCommand]
        private async Task ClearFilter()
        {
            IsFiltered = false;
            await LoadDataAsync();
        }

        [RelayCommand]
        private async Task NewBox()
        {
            await createNewBox();
        }

        private async Task createNewBox()
        {
            var navigation = ServiceHelper.GetService<IPageNavigation>();
            if (navigation == null) throw new Exception("Navigation service not found");
            var v = new AddBoxPage();
            var vm = new AddBoxPageViewModel(dataService, dialogService, navigation, logger);
            v.BindingContext = vm;
            await vm.InitData();
            vm.HasBeenCancelled = false;
            vm.NewBoxId = -2;


            await navigation.NativeNavigation.PushModalAsync(v);
            await vm.TaskCompletionSource.Task;

            var succeeded = false;

            if (vm.HasBeenCancelled == false && vm.NewBoxId > 0)
            {
                await LoadDataAsync();
                await sortList().ConfigureAwait(false);
                SelectedBox = BoxList?.FirstOrDefault(b => b.Id == vm.NewBoxId);
                succeeded = true;
            }

            v.BindingContext = null;
            v = null;
            vm = null;

            if (!succeeded) return;
            await navigation.NavigateToPageAsync("P2");

        }

        [RelayCommand]
        private async Task DoubleTap(Box item)
        {
            var nav = ServiceHelper.GetService<IPageNavigation>();
            if (nav == null) return;
            await nav.NavigateToPageAsync("P2");
        }

        [ObservableProperty]
        private bool isNextOk=true;
        [ObservableProperty]
        private bool isPreviousOk=true;

        [RelayCommand(CanExecute = nameof(CanNavigatePrevious))]
        private Task OnPrevious()
        {
            if (BoxList == null || SelectedBox == null)
            {
                return Task.CompletedTask;
            }
            var currentIndex = BoxList.IndexOf(SelectedBox);
            if (currentIndex > 0)
            {
                SelectedBox = BoxList[currentIndex - 1];
                checkIsNextOk();
                checkIsPreviousOk();
            }
            return Task.CompletedTask;
        }

        private bool checkIsPreviousOk()
        {
            IsPreviousOk = (!IsModified) &&
             (BoxList != null && SelectedBox != null && BoxList.IndexOf(SelectedBox) > 0);
            return IsPreviousOk;
        }

        private bool CanNavigatePrevious()
        {
            checkIsNextOk();
            return checkIsPreviousOk();
        }

        [RelayCommand(CanExecute = nameof(CanNavigateNext))]
        private Task OnNext()
        {
            if (BoxList == null) return Task.CompletedTask;
            if (SelectedBox == null) return Task.CompletedTask;
            int currentIndex = BoxList.IndexOf(SelectedBox);
            if (currentIndex < BoxList.Count - 1)
            {
                SelectedBox = BoxList[currentIndex + 1];
                checkIsNextOk();
                checkIsPreviousOk();
            }
            return Task.CompletedTask;
        }

        private bool checkIsNextOk()
        {
            IsNextOk = (!IsModified) &&
             (BoxList != null && SelectedBox != null && BoxList.IndexOf(SelectedBox) < BoxList.Count - 1);
            return IsNextOk;
        }

        private bool CanNavigateNext()
        {
            checkIsPreviousOk();
            return checkIsNextOk();
        }

        partial void OnIsModifiedChanged(bool value)
        {
            // Notify the commands that their CanExecute state might have changed
            PreviousCommand.NotifyCanExecuteChanged();
            NextCommand.NotifyCanExecuteChanged();
        }

        #endregion

        #region detail page
        // Detail part

        [ObservableProperty]
        private bool isModified;

        [ObservableProperty]
        private string description = string.Empty;

        [ObservableProperty]
        private Area selectedArea;

        [ObservableProperty]
        public ObservableCollection<Area> storageAreas = [];

        private async Task initStorage()
        {
            if (StorageAreas == null) StorageAreas = new ObservableCollection<Area>();
            StorageAreas.Clear();
            var sa = await dataService.GetAreasAsync();
            foreach (var item in sa)
            {
                StorageAreas.Add(item);
            }
        }

        [RelayCommand]
        private void EraseArea()
        {
            SelectedArea = null;
        }

        partial void OnDescriptionChanged(string value)
        {
            if (SelectedBox == null) return;
            IsModified = true;
            SelectedBox.Description = value;
        }

        partial void OnSelectedBoxChanged(Box value)
        {
            if (value is null)
            {
                Description = string.Empty;
                SelectedArea = null;
                IsModified = false;
                IsPreviousOk = false;
                IsNextOk = false;
                return;
            }
            Description = value.Description ?? string.Empty;
            SelectedArea = StorageAreas?.FirstOrDefault(a => a.Id == SelectedBox?.AreaCode);
            IsModified = false;
            checkIsNextOk();
            checkIsPreviousOk();
        }

        // ReSharper disable once UnusedParameterInPartialMethod
        partial void OnSelectedAreaChanged(Area oldValue, Area newValue)
        {
            var oldAreaCode = SelectedBox?.AreaCode;
            var newAreaCode = newValue?.Id;
            if (SelectedBox != null) SelectedBox.AreaCode = newAreaCode ?? null;
            IsModified = oldAreaCode != newAreaCode;
        }

        [RelayCommand]
        private async Task RefreshBox()
        {
            if (SelectedBox == null) return;
            IsBusy = true;
            try
            {
                var box = await dataService.GetBoxByIdAsync(SelectedBox.Id);
                if (box != null)
                {
                    SelectedBox.AreaCode = box.AreaCode;
                    SelectedBox.Number = box.Number;
                    SelectedBox.CreatedAt = box.CreatedAt;
                    SelectedBox.Description = box.Description;

                    Description = box.Description ?? string.Empty;
                    SelectedArea = StorageAreas.FirstOrDefault(a => a.Id == SelectedBox?.AreaCode);
                    IsModified = false;
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public void Save()
        {
            IsBusy = true;
            try
            {
                try
                {
                    dataService.SaveBoxAsync(SelectedBox);
                    Description = SelectedBox.Description;
                }
                finally
                {
                    IsBusy = false;
                }
                IsModified = false;
            }
            catch (Exception ex)
            {
                logger.LogErrorAsync(AppLogger.LogLevel.Error, $"Error saving Box entity: {ex.Message}");
                IsModified = false;
            }
        }


        [RelayCommand]
        private async Task DeleteCurrentBox()
        {
            if (SelectedBox == null) return;
            var r =
                await dialogService.ShowMessageWithBooleanResponse("Delete Box",
                    "Are you sure you want to delete this box?",
                    "Yes", "No");
            if (!r) return;
            IsBusy = true;
            try
            {
                var result = await dataService.DeleteBoxByBoxNumber(SelectedBox.Number);
                if (result)
                {
                    SelectedBox = null;
                    await LoadDataAsync();
                    await sortList().ConfigureAwait(false);
                    var navigation = ServiceHelper.GetService<IPageNavigation>();
                    if (navigation == null) throw new Exception("Navigation service not found");
                    // ReSharper disable once RedundantArgumentDefaultValue
                    await navigation.NavigateToPageAsync("P1");
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task EditAreas()
        {
            await areasPage();
        }

        private async Task areasPage()
        {
            if (IsModified)
            {
                await dialogService.ShowMessage("Save Changes!", "Please save the current box before editing areas", "Ok");
                return;
            }
            var ac = SelectedBox?.AreaCode;
            var navigation = ServiceHelper.GetService<IPageNavigation>();
            if (navigation == null) throw new Exception("Navigation service not found");
            var v = new AreaPage();
            var vm = new AreaPageViewModel(dataService, dialogService, navigation, logger);
            v.BindingContext = vm;

            await navigation.NativeNavigation.PushModalAsync(v);
            await vm.TaskCompletionSource.Task;

            await LoadAreasAsync();
            await initStorage();

            if (SelectedBox != null) SelectedBox.AreaCode = ac;
            SelectedArea = StorageAreas?.FirstOrDefault(a => a.Id == SelectedBox?.AreaCode);
            IsModified = false;

            v.BindingContext = null;
            v = null;
            vm = null;

        }

        #endregion

        #region search page
        // Search Part

        [ObservableProperty]
        private BoxSearchMode currentSearchMode = BoxSearchMode.Default;

        [ObservableProperty]
        private List<Area> areas;

        [ObservableProperty]
        private List<Category> categories;

        [ObservableProperty]
        private Category selectedCategory;

        [ObservableProperty]
        private IEnumerable searchModes;

        [ObservableProperty]
        private Area selectedAreaForSearch;

        private async Task LoadCategoriesAsync()
        {
            Categories = await dataService.GetCategoriesAsync();
        }

        private async Task LoadAreasAsync()
        {
            Areas = await dataService.GetAreasAsync();
        }


        [RelayCommand]
        private async Task PerformSearch(string text)
        {
            try
            {
                await ClearFilter();
                IsBusy = true;
                if ((string.IsNullOrWhiteSpace(text) && IsDescriptionSearchMode)
                    || (SelectedCategory == null && IsCategorySearchMode)
                    || (SelectedAreaForSearch == null && IsAreaSearchMode))
                {
                    return;
                }
                else
                {
                    try
                    {
                        var b = new List<Box>(0);
                        switch (CurrentSearchMode)
                        {
                            case BoxSearchMode.Area:
                                b = await dataService.SearchBoxesAreaAsync(SelectedAreaForSearch?.Name);
                                break;
                            case BoxSearchMode.Description:
                                b = await dataService.SearchBoxesDescAsync(text);
                                break;
                        }
                        BoxList = b;
                    }

                    finally
                    {
                        await sortList().ConfigureAwait(false);
                        IsFiltered = true;
                    }
                }
            }
            catch (Exception ex)
            {
                await logger.LogErrorAsync(AppLogger.LogLevel.Error, "Error in PerformSearch: " + ex.Message);
                await dialogService.ShowMessage("Error", ex.Message, "ok");
            }
            finally { IsBusy = false; }
        }

        partial void OnCurrentSearchModeChanged(BoxSearchMode oldValue, BoxSearchMode newValue)
        {
            try
            {
                if (newValue == oldValue) return;
                SelectedCategory = null;
                SelectedAreaForSearch = null;
                switch (newValue)
                {
                    case BoxSearchMode.Description:
                        IsDescriptionSearchMode = true;
                        IsCategorySearchMode = false;
                        IsAreaSearchMode = false;
                        break;
                    case BoxSearchMode.Area:
                        IsDescriptionSearchMode = false;
                        IsCategorySearchMode = false;
                        IsAreaSearchMode = true;
                        break;
                }
                setPlaceHolder();
            }
            catch (Exception ex)
            {
                logger.LogErrorAsync(AppLogger.LogLevel.Error, "Error in OnCurrentSearchModeChanged: " + ex.Message);
            }
        }

        [ObservableProperty]
        private bool isDescriptionSearchMode;

        [ObservableProperty]
        private bool isAreaSearchMode;

        [ObservableProperty]
        private bool isCategorySearchMode;

        [ObservableProperty]
        private string searchPlaceHolder;

        private void setPlaceHolder()
        {
            if (IsCategorySearchMode)
            { SearchPlaceHolder = "Search by Category"; return; }
            if (IsAreaSearchMode)
            { SearchPlaceHolder = "Search by Area"; return; }
            if (IsDescriptionSearchMode)
            {
                SearchPlaceHolder = "Search by Description";
            }
        }

        [RelayCommand]
        private async Task AIReport()
        {
            await createAIReport();
        }

        private async Task createAIReport()
        {
            var navigation = ServiceHelper.GetService<IPageNavigation>();
            if (navigation == null) throw new Exception("Navigation service not found");
            var v = new CategoryReport(logger, dialogService, dataService);
            await navigation.NativeNavigation.PushModalAsync(v);
            await v.TaskCompletionSource.Task;
            v = null;
        }



        #endregion

        #region Tools
        // Tools

        [RelayCommand]
        private async Task BackupDatabase()
        {
            IsBusy = true;
            try
            {
                await dataService.BackupDatabaseAsync();
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task RestoreDatabase()
        {
            var r = await dialogService.ShowMessageWithBooleanResponse("Restore Database",
                "Are you sure you want to restore the database?",
                "Yes", "No");
            if (!r) return;
            IsBusy = true;
            try
            {
                await dataService.RestoreDatabaseAsync();
                await dialogService.ShowMessage("Restart needed", "The App will stop, please restart it.", "Ok");
                Application.Current?.Quit();
                /*await initStorage();
                await LoadCategoriesAsync();
                await LoadAreasAsync();
                await LoadDataAsync();
                sortList();*/
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ShareTextAsync(string text)
        {
            IsBusy = true;
            try
            {
                await Share.RequestAsync(new ShareTextRequest
                {
                    Text = text,
                    Title = "SmallBazar"
                });
            }
            catch (Exception ex)
            {
                var nomMethode = MethodBase.GetCurrentMethod()?.Name;
                await logger.LogErrorAsync(AppLogger.LogLevel.Error, nomMethode + " : " + ex.Message);
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        private async Task ExportData()
        {
            IsBusy = true;
            try
            {
                var text = await dataService.ExportDataAsync();
#if WINDOWS
                await Clipboard.SetTextAsync(text);
                await dialogService.ShowToast("Data copied to clipboard", CommunityToolkit.Maui.Core.ToastDuration.Long);
#elif ANDROID
                await ShareTextAsync(text);
#elif IOS
                await ShareTextAsync(text);
#endif
            }
            catch (Exception ex)
            {
                var nomMethode = MethodBase.GetCurrentMethod()?.Name;
                await logger.LogErrorAsync(AppLogger.LogLevel.Error, nomMethode + " : " + ex.Message);
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        private async Task DeleteLogs()
        {
            var res = await dialogService.ShowMessageWithBooleanResponse("Delete ALL Logs", "Are you sure you want to delete all logs?", "Yes", "No");
            if (res)
            {
                IsBusy = true;
                try
                {
                    await logger.DeleteAllLogs();
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }

        [RelayCommand]
        private async Task SendLogByMail()
        {
            try
            {
                if (!Email.Default.IsComposeSupported)
                {
                    _ = logger.LogErrorAsync(AppLogger.LogLevel.Error, "Email is not supported on this device");
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
                                                                   + "\n" + (await logger.GetCurrentLogContentAsync())
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
            catch (Exception ex)
            {
                await logger.LogErrorAsync(AppLogger.LogLevel.Error, "Error in SendLogByMail: " + ex.Message);
            }
        }

        [ObservableProperty]
        private string version;

        [ObservableProperty]
        private string softDescription;
        [ObservableProperty]
        private string company;
        [ObservableProperty]
        private string product;
        [ObservableProperty]
        private string copyright;

        [RelayCommand]
        private static async Task OpenWebsite()
        {
            await Browser.OpenAsync("https://www.e-naxos.com");

        }

        [RelayCommand]
        private static async Task OpenDotBlog()
        {
            await Browser.OpenAsync("https://www.e-naxos.com/blog");

        }

        [RelayCommand]
        private async Task PopulateDb()
        {
            try
            {
                var ok = await dialogService.ShowMessageWithBooleanResponse("Populate DB", "Do you want to populate the database with test data?", "Yes", "No");
                if (!ok) return;
                var erase = await dialogService.ShowMessageWithBooleanResponse("Erase DB",
                    "Do you want to erase database before creating test data?",
                    "Yes", "No");
                if (erase) await dataService.ResetDatabaseAsync();
                await dataService.PopulateDatabaseWithTestDataAsync();
                await dialogService.ShowMessage("Restart needed", "The App will stop, please restart it.", "Ok");
                Application.Current?.Quit();
            }
            catch (Exception ex)
            {
                await logger.LogErrorAsync(AppLogger.LogLevel.Error, "Error in PopulateDb: " + ex.Message);
            }
        }

        [RelayCommand]
        private async Task ClearDatabase()
        {
            try
            {
                var ok = await dialogService.ShowMessageWithBooleanResponse("Clear DB", "Do you want to clear the database?", "Yes", "No");
                if (!ok) return;
                await dataService.ResetDatabaseAsync();
                await dialogService.ShowMessage("Restart needed", "The App will stop, please restart it.", "Ok");
                Application.Current?.Quit();
            }
            catch (Exception ex)
            {
                await logger.LogErrorAsync(AppLogger.LogLevel.Error, "Error in ClearDatabase: " + ex.Message);
            }
        }

        [RelayCommand]
        private async Task ShowLogs()
        {
            var navigation = ServiceHelper.GetService<IPageNavigation>();
            if (navigation == null) throw new Exception("Navigation service not found");
            var v = new LogPage();
            var vm = new LogPageViewModel(dialogService, logger, navigation);
            v.BindingContext = vm;

            await navigation.NativeNavigation.PushModalAsync(v);
            await vm.TaskCompletionSource.Task;

            v.BindingContext = null;
            v = null;
            vm = null;

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

        [RelayCommand]
        private async Task EnterKey()
        {
            var k = await ApiKeyStorage.GetApiKeyAsync();
            var r = await dialogService.InputTextDialogAsync("Enter API Key", "Enter OpenAI API Key", "", k);
            var ok = await ApiKeyValidator.IsKeyOk(r);
            if (ok)
            {
                await ApiKeyStorage.SaveApiKeyAsync(r);
                await dialogService.ShowMessage("API Key", "API Key has been validated & saved.", "Ok");
            }
            else
            {
                await dialogService.ShowMessage("API Key", "API Key is not valid and has not been saved.", "Ok");
            }
        }


        [RelayCommand]
        private async Task ClearKey()
        {
            var b = await ApiKeyStorage.DeleteApiKey();
            await dialogService.ShowMessage("Clear API Key", b ? "API Key has been deleted." : "API Key has not been deleted.", "Ok");
        }

        [RelayCommand]
        private async Task CheckKey()
        {
            var b = await ApiKeyValidator.IsKeyOk(await ApiKeyStorage.GetApiKeyAsync());
            await dialogService.ShowMessage("API Key Check", b ? "API Key is Ok" : "API Key is not valid", "Ok");
        }
    }

    #endregion
}





