global using CommunityToolkit.Mvvm.ComponentModel;
using SmallBazar.Models;
using SmallBazar.Services;
using SQLite;
using Xunit;

namespace SmallBazar.Data.Tests;

public sealed class DatabaseSchemaTests
{
    [Fact]
    public async Task EmptyDatabaseCanBeInitializedRepeatedlyWithoutLosingData()
    {
        var path = Path.Combine(Path.GetTempPath(), $"SmallBazar-{Guid.NewGuid():N}.db3");
        var database = new SQLiteAsyncConnection(path);
        try
        {
            await DatabaseSchema.InitializeAsync(database);
            var area = new Area { Name = "Test storage" };
            await database.InsertAsync(area);
            var box = new Box { Number = 42, Description = "Test box", AreaCode = area.Id };
            await database.InsertAsync(box);
            await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => DatabaseSchema.InitializeAsync(database)));

            var stored = await database.Table<Box>().FirstAsync();
            Assert.Equal(box.Id, stored.Id);
            Assert.Equal(box.Number, stored.Number);
            Assert.Equal(box.Description, stored.Description);
            Assert.Equal(box.CreatedAt, stored.CreatedAt);
            Assert.Equal(area.Id, stored.AreaCode);
            Assert.Equal("ok", await database.ExecuteScalarAsync<string>("PRAGMA integrity_check"));
        }
        finally
        {
            await database.CloseAsync();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task InvalidDatabasePropagatesAnErrorAndCanBeRetriedAfterRepair()
    {
        var path = Path.Combine(Path.GetTempPath(), $"SmallBazar-{Guid.NewGuid():N}.db3");
        await File.WriteAllTextAsync(path, "This is not a SQLite database.");
        var database = new SQLiteAsyncConnection(path);
        try
        {
            await Assert.ThrowsAnyAsync<Exception>(() => DatabaseSchema.InitializeAsync(database));
            await database.CloseAsync();
            File.Delete(path);
            await DatabaseSchema.InitializeAsync(database);
            Assert.Empty(await database.Table<Box>().ToListAsync());
            Assert.Equal("ok", await database.ExecuteScalarAsync<string>("PRAGMA integrity_check"));
        }
        finally
        {
            await database.CloseAsync();
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(72, true)]
    [InlineData(71, true)]
    [InlineData(30, false)]
    public async Task HistoricalSchemaPreservesBoxesAndCreatesMissingAreas(int count, bool hasAreas)
    {
        var path = Path.Combine(Path.GetTempPath(), $"SmallBazar-{Guid.NewGuid():N}.db3");
        var referenceDirectory = Environment.GetEnvironmentVariable("SMALLBAZAR_REFERENCE_DIRECTORY");
        if (!string.IsNullOrWhiteSpace(referenceDirectory))
        {
            var label = count switch
            {
                72 => "SmallBazar-installed",
                71 => "SmallBazar-existing-backup",
                _ => "BigBazar-previous"
            };
            var source = Directory.GetFiles(Path.Combine(referenceDirectory, label), "*.db3").Single();
            File.Copy(source, path);
        }

        var database = new SQLiteAsyncConnection(path);
        try
        {
            if (string.IsNullOrWhiteSpace(referenceDirectory))
            {
                await database.CreateTableAsync<Box>();
                await database.CreateTableAsync<Category>();
                await database.CreateTableAsync<BoxCat>();
                for (var number = 1; number <= count; number++)
                    await database.InsertAsync(new Box { Number = number, Description = $"Test {number}" });
                if (hasAreas)
                {
                    await database.CreateTableAsync<Area>();
                    for (var number = 1; number <= 11; number++)
                        await database.InsertAsync(new Area { Name = $"Storage {number}" });
                }
            }

            var originalBoxColumns = (await database.GetTableInfoAsync("Boxes")).Select(column => column.Name).ToArray();
            var before = await SnapshotAsync(database, "Boxes", originalBoxColumns);
            var categoriesBefore = await SnapshotAsync(database, "Categories");
            var associationsBefore = await SnapshotAsync(database, "BoxCats");
            var areasBefore = hasAreas ? await SnapshotAsync(database, "Areas") : [];
            await DatabaseSchema.InitializeAsync(database);
            await DatabaseSchema.InitializeAsync(database);

            Assert.Equal(before, await SnapshotAsync(database, "Boxes", originalBoxColumns));
            Assert.Equal(count, await database.Table<Box>().CountAsync());
            Assert.Equal(categoriesBefore, await SnapshotAsync(database, "Categories"));
            Assert.Equal(associationsBefore, await SnapshotAsync(database, "BoxCats"));
            Assert.Equal(areasBefore, await SnapshotAsync(database, "Areas"));
            Assert.Equal(hasAreas ? 11 : 0, await database.Table<Area>().CountAsync());
            Assert.Equal("ok", await database.ExecuteScalarAsync<string>("PRAGMA integrity_check"));
        }
        finally
        {
            await database.CloseAsync();
            File.Delete(path);
        }
    }

    internal static async Task<string[]> SnapshotAsync(SQLiteAsyncConnection database, string table, string[] columnNames = null)
    {
        var columns = await database.GetTableInfoAsync(table);
        var expressions = (columnNames ?? columns.Select(column => column.Name).ToArray())
            .Select(name => $"quote(\"{name}\")");
        var query = $"SELECT {string.Join(" || '|' || ", expressions)} AS Value FROM \"{table}\" ORDER BY Id";
        return (await database.QueryAsync<SnapshotRow>(query)).Select(row => row.Value).ToArray();
    }

    public sealed class SnapshotRow
    {
        public string Value { get; set; }
    }
}
