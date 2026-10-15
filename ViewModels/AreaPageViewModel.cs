using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using SmallBazar.Models;
using SmallBazar.Services;

namespace SmallBazar.ViewModels
{
    public partial class AreaPageViewModel : BaseViewModel
    {
        private readonly IDataService dataService;
        private readonly IDialogService dialogService;
        private readonly IPageNavigation navigation;
        private readonly IAppLogger logger;
        public TaskCompletionSource TaskCompletionSource { get; set; }
        public AreaPageViewModel(IDataService data, IDialogService dialog, IPageNavigation nav, IAppLogger log)
        {
            dataService = data;
            dialogService = dialog;
            navigation = nav;
            logger = log;
            TaskCompletionSource = new TaskCompletionSource();
            InitData();
        }

        [ObservableProperty]
        private ObservableCollection<AreaDisplayable> areas = [];

        private async void InitData()
        {
            try
            {
                Areas.Clear();
                SelectedArea = null;
                var data = await dataService.GetAreasAsync();
                foreach (var item in data)
                {
                    Areas.Add(new AreaDisplayable(item));
                }
            }
            catch (Exception ex)
            {
                await logger.LogErrorAsync(AppLogger.LogLevel.Error, ex.Message);
            }
        }

        [ObservableProperty]
        private AreaDisplayable selectedArea;


        [RelayCommand]
        private async Task DeleteSelectedArea()
        {
            if (SelectedArea == null) return;
            try
            {
                var usage = await dataService.GetCountUsageForAreaId(SelectedArea.Id);
                if (usage > 0)
                {
                    await dialogService.ShowMessage("Error", 
                        $"Cannot delete the area because it is used in {usage} box(es)", "Ok");
                    return;
                }
                await dataService.DeleteAreaAsync(SelectedArea.Id);
            } catch (Exception ex) 
            {
                await logger.LogErrorAsync(AppLogger.LogLevel.Error, ex.Message);
                await dialogService.ShowMessage("Error", "Could not delete the area","Ok");
                return;
            }
            Areas.Remove(SelectedArea);
            SelectedArea = null;
        }

        [RelayCommand]
        private async Task AddArea()
        {
            var s = await dialogService.InputTextDialogAsync("Add Area", "Enter the name of the new area","new area name");
            if (string.IsNullOrWhiteSpace(s)) return;
            s = s.Trim();
            var newArea = new Area {Name=s };
            try
            {
                newArea.Id = await dataService.AddAreaAsync(newArea);

                var added = false;
                for (var i = 0; i < Areas.Count; i++)
                {
                    if (String.CompareOrdinal(Areas[i].Name, newArea.Name) > 0)
                    {
                        Areas.Insert(i, new AreaDisplayable(newArea));
                        added = true;
                        break;
                    }
                }
                if (!added)
                {
                    Areas.Add(new AreaDisplayable(newArea));
                }
                SelectedArea = new AreaDisplayable(newArea);


            } catch (Exception ex)
            {
                await logger.LogErrorAsync(AppLogger.LogLevel.Error, ex.Message);
                await dialogService.ShowMessage("Error", "Could not add the area", "Ok");
            }
        }

        [RelayCommand]
        private async Task ModifySelectedArea()
        {
            if (SelectedArea == null) return;
            var s = await dialogService.InputTextDialogAsync("Modify Area", "Enter area new name", "New area name",SelectedArea.Name);
            if (string.IsNullOrWhiteSpace(s)) return;
            s = s.Trim();
            SelectedArea.Name = s;
            try
            {
                await dataService.SaveAreaAsync(SelectedArea.ToDataArea());
            } catch (Exception ex)
            {
                await logger.LogErrorAsync(AppLogger.LogLevel.Error, ex.Message);
                await dialogService.ShowMessage("Error", "Could not save the area", "Ok");
            }
        }

        [ObservableProperty]
        private bool isRefreshing;

        [RelayCommand]
        private void RefreshList()
        {
            // IsRefreshing = true; automatically set by the RefreshView
            try
            {
                InitData();
            }
            finally
            {
                IsRefreshing = false; // must be done in a finally block
            }
        }

        [RelayCommand]
        private async Task Close()
        {
            _ = await navigation.NativeNavigation.PopModalAsync();
            TaskCompletionSource.SetResult();
        }

    }

   
}
