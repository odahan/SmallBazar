#nullable disable
using System.Globalization;
using System.Text;
using SmallBazar.Models;
using SmallBazar.Utils;
using SQLite;

namespace SmallBazar.Services;


public class DataService : IDataService
{
    #region private fields
    private const string DatabaseName = "BigBazar.db3";
    private const string DatabaseNameBackup = "SmallBazarDatabaseBackup";
    private readonly DatabaseStore store;
    private SQLiteAsyncConnection database => store.Connection;
    private readonly IAppLogger logger;
    private readonly IDialogService dialogService;
    #endregion

    #region public fields, consts & properties
    public const SQLiteOpenFlags Flags = DatabaseStore.OpenFlags;

    public bool IsInitialized => store.IsInitialized;

    #endregion


    #region Ctor
    public DataService(IAppLogger logger, IDialogService dialog)
    {
        this.logger = logger;
        dialogService = dialog;
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, DatabaseName);
        store = new DatabaseStore(dbPath);
    }

    /// <summary>
    /// Initializes the serialized connection and allows retry after a failure.
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            await store.InitializeAsync();
        }
        catch (Exception ex)
        {
            await logger.LogErrorAsync(AppLogger.LogLevel.Error, "Database initialization failed: " + ex);
            throw;
        }
    }

    /// <summary>
    /// Ensures the connection and its tables are ready before maintenance operations.
    /// </summary>
    public Task CreateTablesAsync() => InitializeAsync();

    #endregion

    #region Box operations
    // Save or update a Box
    public async Task<int> SaveBoxAsync(Box box)
    {
        using var lease = await store.AcquireAsync();
        if (box == null) return -1; 
        box.CreatedAt = DateTime.Now; // 1.5.5 creation date is now the last modification date
        box.Description = box.Description?.Trim();
        int id;
        try
        {
            if (box.Id > 0)
            {
                await database.UpdateAsync(box);
                id = box.Id;
            }
            else
            {
                _ = await database.InsertAsync(box);
                id = box.Id;
            }
        }
        catch (Exception ex)
        {
            await logger.LogErrorAsync(AppLogger.LogLevel.Error, "Error saving box: " + ex.Message);
            return -1;
        }
        return id;
    }


    public async Task<bool> DeleteBoxByBoxNumber(int boxNumber)
    {
        using var lease = await store.AcquireAsync();
        try
        {
            var box = await database.Table<Box>().Where(b => b.Number == boxNumber).FirstOrDefaultAsync();
            if (box != null)
            {
                await database.Table<BoxCat>().DeleteAsync(bc => bc.BoxId == box.Id);
                await database.DeleteAsync<Box>(box.Id);
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            await logger.LogErrorAsync(AppLogger.LogLevel.Error, "Error deleting box by number: " + ex.Message);
            return false;
        }
    }

    // Retrieve all Boxes
    public async Task<List<Box>> GetBoxesAsync()
    {
        using var lease = await store.AcquireAsync();
        try
        {
            return await database.Table<Box>().OrderBy((b) => b.Number).ToListAsync();
        }
        catch (Exception ex)
        {
            await logger.LogErrorAsync(AppLogger.LogLevel.Error, "Error retrieving boxes: " + ex.Message);
            return [];
        }
    }

    // Retrieve all Boxes matching a search text
    public async Task<List<Box>> SearchBoxesDescAsync(string text)
    {
        using var lease = await store.AcquireAsync();
        if (string.IsNullOrWhiteSpace(text)) return await database.Table<Box>().OrderBy(b => b.Number).ToListAsync();

        try
        {
            // VÃ©rifier que la connexion Ã  la base de donnÃ©es n'est pas null
            if (database == null)
            {
                await logger.LogErrorAsync(AppLogger.LogLevel.Error, "Database connection is null.");
                return [];
            }

            // RÃ©cupÃ©rer les boÃ®tes de la base de donnÃ©es
            var boxes = await database.Table<Box>().ToListAsync();

            // VÃ©rifier que la liste des boÃ®tes n'est pas null
            if (boxes == null)
            {
                await logger.LogErrorAsync(AppLogger.LogLevel.Error, "Retrieved boxes list is null.");
                return [];
            }

            // Filtrer et trier les boÃ®tes
            var filteredBoxes = boxes
                .Where(box => Tools.CleanString(box.Description ?? string.Empty)
                    .Contains(Tools.CleanString(text), StringComparison.OrdinalIgnoreCase))
                .OrderBy(b => b.Number)
                .ToList();

            return filteredBoxes;
        }
        catch (Exception ex)
        {
            await logger.LogErrorAsync(AppLogger.LogLevel.Error, $"Error searching boxes on text [{text}]: " + ex.Message);
            return new List<Box>();
        }
    }

    // retrieve all boxes having a category matching the search text
    public async Task<List<Box>> SearchBoxesCatAsync(string text)
    {
        using var lease = await store.AcquireAsync();
        if (string.IsNullOrWhiteSpace(text)) return await database.Table<Box>().OrderBy(b => b.Number).ToListAsync();
        try
        {
            var allBoxes = await database.Table<Box>().ToListAsync();
            var allBoxCats = await database.Table<BoxCat>().ToListAsync();
            var allCats = await database.Table<Category>().ToListAsync();

            var boxes = (from b in allBoxes
                         join bc in allBoxCats on b.Id equals bc.BoxId
                         join c in allCats on bc.CatId equals c.Id
                         where Tools.CleanString(c.Name).Contains(Tools.CleanString(text), StringComparison.OrdinalIgnoreCase)
                         select b).Distinct().OrderBy(b => b.Number).ToList();
            return boxes;
        }
        catch (Exception ex)
        {
            await logger.LogErrorAsync(AppLogger.LogLevel.Error, $"Error searching boxes on category text [{text}]: " + ex.Message);
            return [];
        }

    }

    // Retrieve all boxes having an area matching the search text
    public async Task<List<Box>> SearchBoxesAreaAsync(string text)
    {
        using var lease = await store.AcquireAsync();
        if (string.IsNullOrWhiteSpace(text)) return await database.Table<Box>().OrderBy(b => b.Number).ToListAsync();

        try
        {
            // VÃ©rifier que la connexion Ã  la base de donnÃ©es n'est pas null
            if (database == null)
            {
                await logger.LogErrorAsync(AppLogger.LogLevel.Error, "Database connection is null.");
                return new List<Box>();
            }

            // RÃ©cupÃ©rer toutes les areas
            var areas = await database.Table<Area>().ToListAsync();
            Area areaFound = null;
            var textToSearch = Tools.CleanString(text);

            // Utiliser une boucle for pour trouver l'area correspondant au texte
            foreach (var t in areas)
            {
                if (Tools.CleanString(t.Name) != textToSearch) continue;
                areaFound = t;
                break;
            }

            if (areaFound == null) return await database.Table<Box>().OrderBy(b => b.Number).ToListAsync();

            // RÃ©cupÃ©rer les boÃ®tes correspondant Ã  l'area
            var boxes = await database.Table<Box>()
                .Where(b => b.AreaCode == areaFound.Id)
                .OrderBy(b => b.Number)
                .ToListAsync();

            return boxes;
        }
        catch (Exception ex)
        {
            await logger.LogErrorAsync(AppLogger.LogLevel.Error, $"Error searching boxes on Area [{text}]: " + ex.Message);
            return new List<Box>();
        }
    }



    // Retrieve a Box by ID
    public async Task<Box> GetBoxByIdAsync(int id)
    {
        using var lease = await store.AcquireAsync();
        try
        {
            return await database.Table<Box>().Where(x => x.Id == id).FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            await logger.LogErrorAsync(AppLogger.LogLevel.Error, "Error retrieving box by ID: " + ex.Message);
            return null;
        }
    }

    // Retrieve a Box by number
    public async Task<Box> GetBoxByNumberAsync(int id)
    {
        using var lease = await store.AcquireAsync();
        try
        {
            return await database.Table<Box>().Where(x => x.Number == id).FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            await logger.LogErrorAsync(AppLogger.LogLevel.Error, "Error retrieving box by number: " + ex.Message);
            return null;
        }
    }

    public async Task<int> GetLastBoxNumberAsync()
    {
        using var lease = await store.AcquireAsync();
        try
        {
            var lastBox = await database.Table<Box>().OrderByDescending(b => b.Number).FirstOrDefaultAsync();
            return lastBox?.Number ?? 0;
        }
        catch (Exception ex)
        {
            await logger.LogErrorAsync(AppLogger.LogLevel.Error, "Error retrieving last box number: " + ex.Message);
            return -1;
        }
    }

    public async Task<int> GetNewBoxNumber()
    {
        using var lease = await store.AcquireAsync();
        try
        {
            var firstMissing = await GetFirstMissingBoxNumberCoreAsync();
            if (firstMissing > 0)
            {
                return firstMissing;
            }

            var lastBox = await database.QueryAsync<int>("SELECT Number FROM Boxes ORDER BY Number DESC");
            var lastBoxNumber = lastBox.FirstOrDefault();
            return lastBoxNumber + 1;
        }
        catch (Exception ex)
        {
            await logger.LogErrorAsync(AppLogger.LogLevel.Error, "Error retrieving first available box number: " + ex.Message);
            return -1;
        }
    }

    public async Task<int> GetFirstMissingBoxNumbersAsync()
    {
        using var lease = await store.AcquireAsync();
        try
        {
            return await GetFirstMissingBoxNumberCoreAsync();
        }
        catch (Exception ex)
        {
            await logger.LogErrorAsync(AppLogger.LogLevel.Error, "Error searching first missing Box numbers: " + ex.Message);
            return -1;
        }
    }

    /// <summary>
    /// Finds a gap while the caller owns the database access lease.
    /// </summary>
    private async Task<int> GetFirstMissingBoxNumberCoreAsync()
    {
        var numbers = (await database.Table<Box>().ToListAsync()).Select(b => b.Number).OrderBy(n => n).ToArray();
        for (var index = 0; index < numbers.Length - 1; index++)
        {
            if (numbers[index] + 1 != numbers[index + 1]) return numbers[index] + 1;
        }
        return -1;
    }

    public async Task<List<int>> GetAllActiveBoxNumbers()
    {
        using var lease = await store.AcquireAsync();
        try
        {
            return await database.QueryAsync<int>("SELECT Number FROM Boxes ORDER BY Number ASC");
        }
        catch (Exception ex)
        {
            await logger.LogErrorAsync(AppLogger.LogLevel.Error, "Error retrieving all active box numbers: " + ex.Message);
            await dialogService.ShowMessage("Error", "Error retrieving all active box numbers: " + ex.Message, "OK");
            return [];
        }
    }

    #endregion

    #region Category operations
    // Save or update a Category
    public async Task<int> SaveCategoryAsync(Category category)
    {
        using var lease = await store.AcquireAsync();
        if (category == null) return -1; 
        if (category.Id > 0)
        {
            try
            {
                await database.UpdateAsync(category);
                return category.Id;
            }
            catch (Exception ex)
            {
                await logger.LogErrorAsync(AppLogger.LogLevel.Error, "Error updating category: " + ex.Message);
                return -1;
            }
        }
        else
        {
            try
            {
                return await database.InsertAsync(category);
            }
            catch (Exception ex)
            {
                await logger.LogErrorAsync(AppLogger.LogLevel.Error, "Error inserting category: " + ex.Message);
                return -1;
            }
        }
        //return -1;
    }

    public async Task<int> HowMuchTimesCategoryIsUsed(int categoryId)
    {
        using var lease = await store.AcquireAsync();
        try
        {
            return await database.Table<BoxCat>().CountAsync(bc => bc.CatId == categoryId);
        }
        catch (Exception ex)
        {
            await logger.LogErrorAsync(AppLogger.LogLevel.Error, "Error counting category usage: " + ex.Message);
            return -1;
        }
    }

    // Delete a Category and related BoxCats
    public async Task<int> DeleteCategoryAsync(int categoryId)
    {
        using var lease = await store.AcquireAsync();
        try
        {
            await database.Table<BoxCat>().DeleteAsync(bc => bc.CatId == categoryId);
            return await database.DeleteAsync<Category>(categoryId);
        }
        catch (Exception ex)
        {
            await logger.LogErrorAsync(AppLogger.LogLevel.Error, "Error deleting category: " + ex.Message);
            return -1;
        }
    }

    // Retrieve all Categories
    public async Task<List<Category>> GetCategoriesAsync()
    {
        using var lease = await store.AcquireAsync();
        try
        {
            return await database.Table<Category>().OrderBy((c) => c.Name).ToListAsync();
        }
        catch (Exception ex)
        {
            await logger.LogErrorAsync(AppLogger.LogLevel.Error, "Error retrieving categories: " + ex.Message);
            return [];
        }
    }

    // Retrieve a Category by ID
    public async Task<Category> GetCategoryByIdAsync(int id)
    {
        using var lease = await store.AcquireAsync();
        try
        {
            return await database.Table<Category>().Where(x => x.Id == id).FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            await logger.LogErrorAsync(AppLogger.LogLevel.Error, "Error retrieving category by ID: " + ex.Message);
            return null;
        }
    }
    #endregion

    #region BoxCat operations

    // save or update a BoxCat relationship 
    public async Task<int> SaveBoxCatAsync(BoxCat boxCat)
    {
        using var lease = await store.AcquireAsync();
        if (boxCat == null) return -1;
        if (boxCat.Id > 0)
        {
            try
            {
                await database.UpdateAsync(boxCat);
                return boxCat.Id;
            }
            catch (Exception ex)
            {
                await logger.LogErrorAsync(AppLogger.LogLevel.Error, "Error updating BoxCat: " + ex.Message);
                return -1;
            }
        }
        else
        {
            try
            {
                return await database.InsertAsync(boxCat);

            }
            catch (Exception ex)
            {
                await logger.LogErrorAsync(AppLogger.LogLevel.Error, "Error inserting BoxCat: " + ex.Message);
                return -1;
            }
        }
        //return -1;
    }

    // Add a new BoxCat relationship
    public async Task<int> AddBoxCatAsync(BoxCat boxCat)
    {
        using var lease = await store.AcquireAsync();
        if (boxCat == null) return -1;
        try
        {
            await database.InsertAsync(boxCat);
            return boxCat.Id;
        }
        catch (Exception ex)
        {
            await logger.LogErrorAsync(AppLogger.LogLevel.Error, "Error inserting BoxCat: " + ex.Message);
            return -1;
        }
    }

    // Delete a BoxCat relationship
    public async Task<int> DeleteBoxCatAsync(int boxCatId)
    {
        using var lease = await store.AcquireAsync();
        try
        {
            return await database.DeleteAsync<BoxCat>(boxCatId);
        }
        catch (Exception ex)
        {
            await logger.LogErrorAsync(AppLogger.LogLevel.Error, "Error deleting BoxCat: " + ex.Message);
            return -1;
        }
    }

    // Retrieve all BoxCat relationships
    public async Task<List<BoxCat>> GetBoxCatsAsync()
    {
        using var lease = await store.AcquireAsync();
        try
        {
            return await database.Table<BoxCat>().ToListAsync();
        }
        catch (Exception ex)
        {
            await logger.LogErrorAsync(AppLogger.LogLevel.Error, "Error retrieving BoxCat relationships: " + ex.Message);
            return [];
        }
    }

    #endregion

    #region Crud operations for Area entity
    public async Task<int> SaveAreaAsync(Area area)
    {
        using var lease = await store.AcquireAsync();
        if (area == null) return -1;
        if (area.Id > 0)
        {
            try
            {
                await database.UpdateAsync(area);
                return area.Id;
            }
            catch (Exception ex)
            {
                await logger.LogErrorAsync(AppLogger.LogLevel.Error, "Error updating Areas: " + ex.Message);
                return -1;
            }
        }
        else
        {
            try
            {
                return await database.InsertAsync(area);

            }
            catch (Exception ex)
            {
                await logger.LogErrorAsync(AppLogger.LogLevel.Error, "Error inserting Area: " + ex.Message);
                return -1;
            }
        }
        //return -1;
    }

    public async Task<int> AddAreaAsync(Area area)
    {
        using var lease = await store.AcquireAsync();
        if (area == null) return -1;
        try
        {
            await database.InsertAsync(area);
            return area.Id;
        }
        catch (Exception ex)
        {
            await logger.LogErrorAsync(AppLogger.LogLevel.Error, "Error inserting Area: " + ex.Message);
            return -1;
        }
    }

    public async Task<int> DeleteAreaAsync(int areaId)
    {
        using var lease = await store.AcquireAsync();
        try
        {
            return await database.DeleteAsync<Area>(areaId);
        }
        catch (Exception ex)
        {
            await logger.LogErrorAsync(AppLogger.LogLevel.Error, "Error deleting Area: " + ex.Message);
            return -1;
        }
    }

    public async Task<List<Area>> GetAreasAsync()
    {
        using var lease = await store.AcquireAsync();
        try
        {
            return await database.Table<Area>().OrderBy(o => o.Name).ToListAsync();
        }
        catch (Exception ex)
        {
            await logger.LogErrorAsync(AppLogger.LogLevel.Error, "Error retrieving Areas: " + ex.Message);
            return [];
        }
    }

    public async Task<Area> GetAreaForBoxId(int boxId)
    {
        using var lease = await store.AcquireAsync();
        try
        {
            var box = await database.Table<Box>().Where(b => b.Id == boxId).FirstOrDefaultAsync();
            if (box != null)
            {
                return await database.Table<Area>().FirstOrDefaultAsync(a => a.Id == box.AreaCode);
            }
        }
        catch (Exception ex)
        {
            await logger.LogErrorAsync(AppLogger.LogLevel.Error, $"Error retrieving Area for Box Id {boxId}: " + ex.Message);
            return null;
        }
        return null;
    }

    public async Task<Area> GetAreaById(int areaCode)
    {
        using var lease = await store.AcquireAsync();
        try
        {
            return await database.Table<Area>().FirstOrDefaultAsync(a => a.Id == areaCode);
        }
        catch (Exception ex)
        {
            await logger.LogErrorAsync(AppLogger.LogLevel.Error, $"Error retrieving Area for code {areaCode}: " + ex.Message);
            return null;
        }
    }

    public async Task<int> GetCountUsageForAreaId(int areaCode)
    {
        using var lease = await store.AcquireAsync();
        try
        {
            return await database.Table<Box>().CountAsync(b => b.AreaCode == areaCode);
        }
        catch (Exception ex)
        {
            await logger.LogErrorAsync(AppLogger.LogLevel.Error, $"Error counting usage for Area code {areaCode}: " + ex.Message);
            return int.MaxValue;
        }
    }
    #endregion

    #region Backup database

    /// <summary>
    /// Creates a consistent snapshot before exporting it through the platform UI.
    /// </summary>
    public async Task<bool> BackupDatabaseAsync()
    {
        try
        {
            var backupName = $"{DatabaseNameBackup}-{DateTime.UtcNow:yyyyMMddTHHmmssfffffffZ}-{Guid.NewGuid():N}.db3";
            string destination = null;
#if WINDOWS
            var folder = await MainThread.InvokeOnMainThreadAsync(WindowsDatabasePicker.PickBackupFolderAsync);
            if (folder == null) return false;
            destination = Path.Combine(folder, backupName);
#endif

            var snapshot = Path.Combine(FileSystem.CacheDirectory, backupName);
            await store.BackupAsync(snapshot);
            if (destination != null)
            {
                File.Copy(snapshot, destination, false);
                File.Delete(snapshot);
                await dialogService.ShowMessage("Backup saved", $"The database backup was saved to:\n{destination}", "OK");
            }
            else
            {
                await MainThread.InvokeOnMainThreadAsync(() => Share.RequestAsync(new ShareFileRequest
                {
                    Title = "Bazar Database",
                    File = new ShareFile(snapshot)
                }));
            }
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            await logger.LogErrorAsync(AppLogger.LogLevel.Error, "Error creating backup: " + ex);
            await dialogService.ShowMessage("Backup failed", ex.Message, "OK");
            return false;
        }
    }

    /// <summary>
    /// Reads a selected backup as a stream and replaces data only after validation and recovery backup.
    /// </summary>
    public async Task<bool> RestoreDatabaseAsync()
    {
        try
        {
#if WINDOWS
            await using var source = await MainThread.InvokeOnMainThreadAsync(WindowsDatabasePicker.PickRestoreStreamAsync);
            if (source == null) return false;
#else
            var result = await MainThread.InvokeOnMainThreadAsync(() => FilePicker.PickAsync(
                new PickOptions { PickerTitle = "Select a SmallBazar database backup" }));
            if (result == null) return false;

            await using var source = await result.OpenReadAsync();
#endif
            var recoveryPath = await store.RestoreAsync(source);
            await logger.LogErrorAsync(AppLogger.LogLevel.Information, "Database restored. Recovery copy: " + recoveryPath);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            await logger.LogErrorAsync(AppLogger.LogLevel.Error, "Error restoring database: " + ex);
            await dialogService.ShowMessage("Restore failed", ex.Message, "OK");
            return false;
        }
    }

    public async Task<string> ExportDataAsync()
    {
        using var lease = await store.AcquireAsync();
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine($"SmallBazar Data Export on {DateTime.Now.ToString(new CultureInfo("en-US"))}");
            sb.AppendLine("Box number\tDescription\tArea");

            // RÃ©cupÃ©rer toutes les boÃ®tes
            var boxes = await database.Table<Box>()
                .OrderBy(b => b.Number)
                .ToListAsync();

            foreach (var box in boxes)
            {
                sb.Append($"{box.Number} + '\t' + {box.DescriptionForDisplay}" + '\t');

                // area
                if (box.AreaCode != null)
                {
                    var area = await database.Table<Area>().FirstOrDefaultAsync(a => a.Id == box.AreaCode);
                    if (area != null)
                    {
                        sb.Append($"{area.Name}");
                    }
                } else
                {
                    sb.Append("Unknown");
                }

                /*
                // categories
                var boxCats = await database.Table<BoxCat>().Where(bc => bc.BoxId == box.Id).ToListAsync();

                if (boxCats.Count > 0)
                {
                    sb.Append("; (");
                }

                var first = true;

                foreach (var boxCat in boxCats)
                {
                    var category = await database.Table<Category>().FirstOrDefaultAsync(c => c.Id == boxCat.CatId);
                    if (category != null)
                    {
                        sb.Append(first ? "" : ", ");
                        first = false;
                        sb.Append($"{category.Name}");
                    }
                }

                if (boxCats.Count > 0)
                {
                    sb.Append(")");
                }
                */

                sb.AppendLine();
            }
            return sb.ToString().Trim();

        }
        catch (Exception ex)
        {
            await logger.LogErrorAsync(AppLogger.LogLevel.Error, "Error exporting data: " + ex.Message);
            return string.Empty;
        }
    }

    #endregion

    
    #region For Testing purpose
    // For testing & maintenance purpose

    /// <summary>
    /// Recreates the database while excluding concurrent reads and writes.
    /// </summary>
    public Task ResetDatabaseAsync() => store.ResetAsync();

    // Populate the database with test data and generate test images
    public async Task PopulateDatabaseWithTestDataAsync()
    {
        using var lease = await store.AcquireAsync();
        try
        {
            var syllables = new List<string> { "lo", "rem", "ip", "sum", "do", "lor", "sit", "a", "met", "con", "sec", "te", "tur", "ad", "i", "pis", "cing", "e", "lit", "sed", "do", "eius", "mod", "tem", "por", "in", "ci", "di", "dunt", "ut", "la", "bo", "re", "et", "do", "lo", "re", "mag", "na", "a", "li", "qua" };
            const int MaxItems = 30;
            const int MaxAreas = 10;
            var random = new Random();
            // add test data to the database
            for (var i = 1; i <= MaxAreas; i++)
            {
                var randomWord = string.Concat(syllables[random.Next(syllables.Count)], syllables[random.Next(syllables.Count)], syllables[random.Next(syllables.Count)]) + " Area";
                await database.InsertAsync(new Area { Name = $"{randomWord} {i}" });
            }

            for (var i = 1; i <= MaxItems; i++)
            {
                var randomword = string.Empty;
                for (var y = 0; y < random.Next(3, 30); y++)
                {
                    randomword += string.Concat(syllables[random.Next(syllables.Count)], syllables[random.Next(syllables.Count)], syllables[random.Next(syllables.Count)]) + " ";
                }
                randomword = randomword.Trim();
                int? areaCode = random.Next(MaxAreas);
                if (random.Next(100) < 50) areaCode = null;
                await database.InsertAsync(new Box { Number = i, Description = $"{randomword} {i}", AreaCode = areaCode });
                randomword = string.Concat(syllables[random.Next(syllables.Count)], syllables[random.Next(syllables.Count)], syllables[random.Next(syllables.Count)]);
                await database.InsertAsync(new Category { Name = $"{randomword} {i}" });

            }

            // add BoxCat relationships
            for (var i = 1; i <= MaxItems * 4; i++)
            {
                var boxCat = new BoxCat { BoxId = i % MaxItems, CatId = random.Next(MaxItems) + 1 };
                await database.InsertAsync(boxCat);
            }
        }
        catch (Exception ex)
        {
            await logger.LogErrorAsync(AppLogger.LogLevel.Error, "Error populating database with test data: " + ex.Message);
            await dialogService.ShowMessage("Error", "Error populating database with test data: " + ex.Message, "OK");
        }
    }
    #endregion


}
