using SmallBazar.Models;
using SQLite;

namespace SmallBazar.Services;

/// <summary>
/// Versions the business schema while preserving boxes and storage areas.
/// </summary>
internal static class DatabaseSchema
{
    internal const int CurrentVersion = 1;

    /// <summary>
    /// Backs up unversioned databases before migrating all schema changes in one transaction.
    /// </summary>
    public static async Task InitializeAsync(SQLiteAsyncConnection database)
    {
        var version = await database.ExecuteScalarAsync<int>("PRAGMA user_version");
        if (version > CurrentVersion)
            throw new InvalidDataException($"Database schema version {version} is newer than supported version {CurrentVersion}.");

        if (version < CurrentVersion)
        {
            var tables = await database.QueryScalarsAsync<string>(
                "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%'");
            if (tables.Count > 0)
            {
                var directory = Path.Combine(Path.GetDirectoryName(database.DatabasePath), "DatabaseMigrationBackups");
                Directory.CreateDirectory(directory);
                var backup = Path.Combine(directory,
                    $"before-v{CurrentVersion}-{DateTime.UtcNow:yyyyMMddTHHmmssfffffffZ}-{Guid.NewGuid():N}.db3");
                await database.BackupAsync(backup);
                var snapshot = new SQLiteAsyncConnection(backup, SQLiteOpenFlags.ReadOnly | SQLiteOpenFlags.FullMutex);
                try
                {
                    var integrity = await snapshot.QueryScalarsAsync<string>("PRAGMA integrity_check");
                    if (integrity.Count != 1 || integrity[0] != "ok")
                        throw new InvalidDataException($"The migration backup failed its integrity check: {backup}");
                }
                finally
                {
                    await snapshot.CloseAsync();
                }
            }

            await database.RunInTransactionAsync(connection =>
            {
                connection.CreateTable<Box>();
                connection.CreateTable<Area>();
                connection.Execute("DROP TABLE IF EXISTS BoxCats");
                connection.Execute("DROP TABLE IF EXISTS Categories");
                connection.Execute($"PRAGMA user_version = {CurrentVersion}");
            });
        }
        await database.ExecuteAsync("PRAGMA case_sensitive_like = OFF");
    }
}
