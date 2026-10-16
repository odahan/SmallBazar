using SmallBazar.Models;
using SQLite;

namespace SmallBazar.Services;

/// <summary>
/// Creates the historical schema without replacing existing data.
/// </summary>
internal static class DatabaseSchema
{
    /// <summary>
    /// Awaits every schema operation before the database can be used.
    /// </summary>
    public static async Task InitializeAsync(SQLiteAsyncConnection database)
    {
        await database.CreateTableAsync<Box>();
        await database.CreateTableAsync<Category>();
        await database.CreateTableAsync<BoxCat>();
        await database.CreateTableAsync<Area>();
        await database.ExecuteAsync("PRAGMA case_sensitive_like = OFF");
    }
}
