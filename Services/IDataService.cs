#nullable disable
using SmallBazar.Models;

namespace SmallBazar.Services;

public interface IDataService
{
    // init
    // ReSharper disable once InconsistentNaming
    bool IsInitialized { get; }
    Task InitializeAsync();

    #region operations for Box entity
    Task<int> SaveBoxAsync(Box box);
    Task<List<Box>> GetBoxesAsync();
    Task<Box> GetBoxByIdAsync(int id);
    Task<Box> GetBoxByNumberAsync(int id);
    Task<int> GetLastBoxNumberAsync();
    Task<bool> DeleteBoxByBoxNumber(int boxNumber);
    Task<List<Box>> SearchBoxesDescAsync(string text);
    Task<List<Box>> SearchBoxesAreaAsync(string text);
    Task<int> GetFirstMissingBoxNumbersAsync();
    Task<int> GetNewBoxNumber();
    Task<List<int>> GetAllActiveBoxNumbers();

    #endregion

    #region Crud operations for Area entity
    Task<int> SaveAreaAsync(Area area);
    Task<int> AddAreaAsync(Area area);
    Task<int> DeleteAreaAsync(int areaId);
    Task<List<Area>> GetAreasAsync();
    Task<Area> GetAreaForBoxId(int boxId);
    Task<Area> GetAreaById(int areaCode);
    Task<int> GetCountUsageForAreaId(int areaCode);

    #endregion


    #region Backup database
    Task<bool> BackupDatabaseAsync();
    Task<bool> RestoreDatabaseAsync();
    Task<string> ExportDataAsync();
    #endregion
        
    #region For Init, Maintenance & Testing purpose
    Task CreateTablesAsync();
    Task ResetDatabaseAsync();
    Task PopulateDatabaseWithTestDataAsync();
    #endregion
}
