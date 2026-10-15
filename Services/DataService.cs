#nullable disable
using System.Globalization;
using System.Text;
using CommunityToolkit.Maui.Storage;
using SmallBazar.Models;
using SmallBazar.Utils;
using SQLite;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;

namespace SmallBazar.Services;


public class DataService : IDataService
{
    #region private fields
    private const string DatabaseName = "BigBazar.db3";
    private const string DatabaseNameBackup = "SmallBazarDatabaseBackup";
    private SQLiteAsyncConnection database;
    private readonly string dbPath;
    private readonly IAppLogger logger;
    // ReSharper disable once NotAccessedField.Local
    private readonly IDialogService dialogService;
    #endregion

    #region public fields, consts & properties
    public const SQLiteOpenFlags Flags =
        // open the database in read/write mode
        SQLiteOpenFlags.ReadWrite |
        // create the database if it doesn't exist
        SQLiteOpenFlags.Create |
        // enable multi-threaded database access
        SQLiteOpenFlags.SharedCache;

    public bool IsInitialized { get; private set; }

    #endregion


    #region Ctor
    public DataService(IAppLogger logger, IDialogService dialog)
    {
        logger.LogErrorAsync(AppLogger.LogLevel.Information, "Database Service initializing");
        IsInitialized = false;
        this.logger = logger;
        dialogService = dialog;

                
        // Construct database path

        dbPath = Path.Combine(FileSystem.AppDataDirectory, DatabaseName);
        // ex: /data/user/0/com.companyname.bigbazar/files/BigBazar.db3
        // accessible avec Android Device Explorer dans Android Studio, répertoire data/data/com.companyname.bigbazar/files
        try
        {
            database = new SQLiteAsyncConnection(dbPath, Flags);
        }
        catch (Exception ex)
        {
            logger.LogErrorAsync(AppLogger.LogLevel.Error, "Error SQLite initializing database: " + ex.Message);
            IsInitialized = false;
            return;
        }

        try
        {
            database.CreateTableAsync<Box>();
            database.CreateTableAsync<Category>();
            database.CreateTableAsync<BoxCat>();
            database.CreateTableAsync<Area>();
        }
        catch (Exception ex)
        {
            logger.LogErrorAsync(AppLogger.LogLevel.Error, "Error creating tables: " + ex.Message);
            IsInitialized = false;
            return;
        }

        // add a SQLIte to make search case-insensitive
        database.ExecuteAsync("PRAGMA case_sensitive_like = OFF");

        //logger.LogErrorAsync(AppLogger.LogLevel.Information, "Database Service initialized"+new string('-',30));
        IsInitialized = true;
    }

    public async Task CreateTablesAsync()
    {
        IsInitialized = false;
        try
        {
            await database.CreateTableAsync<Box>();
            await database.CreateTableAsync<Category>();
            await database.CreateTableAsync<BoxCat>();
            await database.CreateTableAsync<Area>();
            IsInitialized = true;
        }
        catch (Exception ex)
        {
            await logger.LogErrorAsync(AppLogger.LogLevel.Error, "Error creating tables: " + ex.Message);
            IsInitialized = false;
        }

    }
    #endregion

    #region Box operations
    // Save or update a Box
    public async Task<int> SaveBoxAsync(Box box)
    {
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
        try
        {
            var box = await GetBoxByNumberAsync(boxNumber);
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
        if (string.IsNullOrWhiteSpace(text)) return await GetBoxesAsync();

        try
        {
            // Vérifier que la connexion à la base de données n'est pas null
            if (database == null)
            {
                await logger.LogErrorAsync(AppLogger.LogLevel.Error, "Database connection is null.");
                return [];
            }

            // Récupérer les boîtes de la base de données
            var boxes = await database.Table<Box>().ToListAsync();

            // Vérifier que la liste des boîtes n'est pas null
            if (boxes == null)
            {
                await logger.LogErrorAsync(AppLogger.LogLevel.Error, "Retrieved boxes list is null.");
                return [];
            }

            // Filtrer et trier les boîtes
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
        if (string.IsNullOrWhiteSpace(text)) return await GetBoxesAsync();
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
        if (string.IsNullOrWhiteSpace(text)) return await GetBoxesAsync();

        try
        {
            // Vérifier que la connexion à la base de données n'est pas null
            if (database == null)
            {
                await logger.LogErrorAsync(AppLogger.LogLevel.Error, "Database connection is null.");
                return new List<Box>();
            }

            // Récupérer toutes les areas
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

            if (areaFound == null) return await GetBoxesAsync();

            // Récupérer les boîtes correspondant à l'area
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
        try
        {
            var firstMissing = await GetFirstMissingBoxNumbersAsync();
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
        try
        {
            var allLines = await database.Table<Box>().ToListAsync().ConfigureAwait(false);
            //var allNumbers = await database.QueryAsync<int>("SELECT Number FROM Boxes ORDER BY Number ASC");
            var allNumbers = allLines.Select(b => b.Number).OrderBy(n => n).ToList();

            for (var i = 0; i < allNumbers.Count - 1; i++)
            {
                if (allNumbers[i] + 1 != allNumbers[i + 1])
                {
                    int? firstMissingNumber = allNumbers[i] + 1;
                    return firstMissingNumber.Value;
                }
            }
            return -1;
        }
        catch (Exception ex)
        {
            await logger.LogErrorAsync(AppLogger.LogLevel.Error, "Error searching first missing Box numbers: " + ex.Message);
            return -1;
        }
    }

    public async Task<List<int>> GetAllActiveBoxNumbers()
    { 
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
        try
        {
            var box = await GetBoxByIdAsync(boxId);
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
    // Backup database and send it via share
    /* public async Task<bool> BackupDatabaseAsync()
     {
         if (database != null)
         {
             await database.CloseAsync();
         }

         return await Task.Run(async () =>
         {

             try
             {
                 // Make sure that all database operations are completed before continuing.
                 var dbCopy = Path.Combine(FileSystem.AppDataDirectory, DatabaseNameBackup);
                 // Copy the database file to the backup file

                 try
                 {
                     File.Copy(dbPath, dbCopy, true);
                     await Share.RequestAsync(new ShareFileRequest
                     {
                         Title = "Bazar Database",
                         File = new ShareFile(dbCopy)
                     });
                 }
                 catch (Exception ex)
                 {
                     await logger.LogErrorAsync(AppLogger.LogLevel.Error, "Error creating backup file: " + ex.Message);
                     await dialogService.ShowMessage("Error creating backup file", ex.Message, "OK");
                     return false;
                 }
                 finally
                 {
                     try
                     {
                         database = new SQLiteAsyncConnection(dbPath, Flags);
                     }
                     catch (Exception ex)
                     {
                         await logger.LogErrorAsync(AppLogger.LogLevel.Error, "Error SQLite creating database: " + ex.Message);
                         IsInitialized = false;
                     }
                 }

                 return true;
             }
             catch (Exception ex)
             {
                 await logger.LogErrorAsync(AppLogger.LogLevel.Error, "Error sending sql backup by share: " + ex.Message);
                 return false;
             }
         });
     }*/


    public async Task<bool> BackupDatabaseAsync()
    {
        if (database != null)
        {
            await database.CloseAsync();
        }

        return await Task.Run(async () =>
        {
            try
            {
                var dbBakName =  DatabaseNameBackup + ((DeviceInfo.Platform == DevicePlatform.WinUI) ? "Win.db3" : "Droid.db3");
                // Make sure that all database operations are completed before continuing.
                var dbCopy = Path.Combine(FileSystem.AppDataDirectory, dbBakName);

                try
                {
                    File.Copy(dbPath, dbCopy, true);

                    if (DeviceInfo.Platform == DevicePlatform.WinUI)
                    {
                        // Use FolderPicker from CommunityToolkit.Maui.Storage to let the user choose the directory
                        var result = await FolderPicker.Default.PickAsync();

                        if (result.IsSuccessful && result.Folder != null)
                        {
                            var backupPath = Path.Combine(result.Folder.Path, dbBakName);
                            File.Copy(dbCopy, backupPath, true);
                        }
                        else
                        {
                            await dialogService.ShowMessage("Backup Canceled", "No folder selected.", "OK");
                            return false;
                        }
                    }
                    else
                    {
                        // Share the file for other platforms
                        await Share.RequestAsync(new ShareFileRequest
                        {
                            Title = "Bazar Database",
                            File = new ShareFile(dbCopy)
                        });
                    }
                }
                catch (Exception ex)
                {
                    await logger.LogErrorAsync(AppLogger.LogLevel.Error, "Error creating backup file: " + ex.Message);
                    await dialogService.ShowMessage("Error creating backup file", ex.Message, "OK");
                    return false;
                }
                finally
                {
                    try
                    {
                        database = new SQLiteAsyncConnection(dbPath, Flags);
                        // add a SQLIte to make search case-insensitive
                        await database.ExecuteAsync("PRAGMA case_sensitive_like = OFF");
                    }
                    catch (Exception ex)
                    {
                        await logger.LogErrorAsync(AppLogger.LogLevel.Error, "Error SQLite creating database: " + ex.Message);
                        IsInitialized = false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                await logger.LogErrorAsync(AppLogger.LogLevel.Error, "Error sending sql backup by share: " + ex.Message);
                return false;
            }
        });
    }



    public async Task<bool> RestoreDatabaseAsync()
    {
        if (database != null)
        {
            await database.CloseAsync();
        }

        return await Task.Run(async () =>
        {
            try
            {
                // Open a file picker to select the backup file
                var result = await FilePicker.PickAsync();

                if (result != null)
                {
                    var backupFilePath = result.FullPath;

                    // Check if the selected file is a .db3 file
                    if (Path.GetExtension(backupFilePath).ToLower() != ".db3")
                    {
                        await dialogService.ShowMessage("Invalid file type", "Please select a .db3 file", "OK");
                        return false;
                    }

                    // Copy the backup file to the database file
                    try
                    {
                        File.Copy(backupFilePath, dbPath, true);
                    }
                    catch (Exception ex)
                    {
                        await logger.LogErrorAsync(AppLogger.LogLevel.Error, "Error restoring backup file: " + ex.Message);
                        await dialogService.ShowMessage("Error restoring backup file", ex.Message, "OK");
                        return false;
                    }
                    finally
                    {
                        try
                        {
                            database = new SQLiteAsyncConnection(dbPath, Flags);
                            // add a SQLIte to make search case-insensitive
                            await database.ExecuteAsync("PRAGMA case_sensitive_like = OFF");
                        }
                        catch (Exception ex)
                        {
                            await logger.LogErrorAsync(AppLogger.LogLevel.Error, "Error SQLite creating database: " + ex.Message);
                            IsInitialized = false;
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                await logger.LogErrorAsync(AppLogger.LogLevel.Error, "Error restoring database: " + ex.Message);
                return false;
            }
        });
    }

    public async Task<string> ExportDataAsync()
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine($"SmallBazar Data Export on {DateTime.Now.ToString(new CultureInfo("en-US"))}");
            sb.AppendLine("Box number\tDescription\tArea");

            // Récupérer toutes les boîtes
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

    // Reset the database and delete all photos
    public async Task ResetDatabaseAsync()
    {
        if (database != null)
        {
            await database.CloseAsync();
        }

        // Delete the database file
        try
        {
            File.Delete(dbPath);
        }
        catch (Exception ex)
        {
            await logger.LogErrorAsync(AppLogger.LogLevel.Error, $"An error occurred while deleting the database: {ex.Message}");
            throw;
        }

        try
        {
            database = new SQLiteAsyncConnection(dbPath, Flags);
            // add a SQLIte to make search case-insensitive
            await database.ExecuteAsync("PRAGMA case_sensitive_like = OFF");
        }
        catch (Exception ex)
        {
            await logger.LogErrorAsync(AppLogger.LogLevel.Error, "Error SQLite creating database: " + ex.Message);
            IsInitialized = false;
            return;
        }

        await CreateTablesAsync(); // Recreate tables
        
    }

    // Populate the database with test data and generate test images
    public async Task PopulateDatabaseWithTestDataAsync()
    {
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
