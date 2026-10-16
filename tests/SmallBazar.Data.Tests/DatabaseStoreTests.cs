using SmallBazar.Models;
using SmallBazar.Services;
using SQLite;
using Xunit;

namespace SmallBazar.Data.Tests;

public sealed class DatabaseStoreTests : IAsyncLifetime
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"SmallBazar-store-{Guid.NewGuid():N}");
    private DatabaseStore store;
    private string PathFor(string name) => Path.Combine(directory, name);

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(directory);
        store = new DatabaseStore(PathFor("BigBazar.db3"));
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await store.DisposeAsync();
        Directory.Delete(directory, true);
    }

    [Fact]
    public async Task BackupIncludesCommittedWalDataAndLeavesConnectionUsable()
    {
        using (await store.AcquireAsync())
        {
            await store.Connection.EnableWriteAheadLoggingAsync();
            await store.Connection.ExecuteScalarAsync<int>("PRAGMA wal_autocheckpoint = 0");
            await store.Connection.InsertAsync(new Box { Number = 42, Description = "Committed WAL data" });
            Assert.True(File.Exists(PathFor("BigBazar.db3-wal")));
        }

        var backup = PathFor("snapshot.db3");
        await store.BackupAsync(backup);
        using (var copied = new SQLiteConnection(backup, SQLiteOpenFlags.ReadOnly))
        {
            Assert.Equal("Committed WAL data", copied.Table<Box>().Single().Description);
            Assert.Equal("ok", copied.ExecuteScalar<string>("PRAGMA integrity_check"));
        }
        await Assert.ThrowsAsync<IOException>(() => store.BackupAsync(backup));
        await Assert.ThrowsAsync<IOException>(() => store.BackupAsync(PathFor("BigBazar.db3")));
        using (await store.AcquireAsync())
        {
            await store.Connection.InsertAsync(new Box { Number = 43, Description = "After backup" });
            Assert.Equal(2, await store.Connection.Table<Box>().CountAsync());
        }
    }

    [Fact]
    public async Task StreamRestorePreservesEveryTableAndKeepsFreshRecoverySnapshot()
    {
        using (await store.AcquireAsync())
            await store.Connection.InsertAsync(new Box { Number = 1, Description = "Before restore" });

        var incomingPath = PathFor("incoming.db3");
        using (var incoming = new SQLiteConnection(incomingPath))
        {
            incoming.CreateTable<Box>();
            incoming.CreateTable<Area>();
            incoming.CreateTable<Category>();
            incoming.CreateTable<BoxCat>();
            incoming.Execute("INSERT INTO Areas (Id, Name) VALUES (7, 'Storage')");
            incoming.Execute("INSERT INTO Boxes (Id, Number, Description, AreaCode, CreatedAt) VALUES (4, 42, 'Restored', 7, ?)",
                new DateTime(2024, 2, 3, 4, 5, 6).Ticks);
            incoming.Execute("INSERT INTO Categories (Id, Name) VALUES (2, 'Historical category')");
            incoming.Execute("INSERT INTO BoxCats (Id, BoxId, CatId) VALUES (8, 4, 2)");
        }

        await using var source = new NonSeekableStream(await File.ReadAllBytesAsync(incomingPath));
        var recovery = await store.RestoreAsync(source);
        using (var saved = new SQLiteConnection(recovery, SQLiteOpenFlags.ReadOnly))
            Assert.Equal("Before restore", saved.Table<Box>().Single().Description);
        using (await store.AcquireAsync())
        {
            var box = await store.Connection.Table<Box>().FirstAsync();
            Assert.Equal(4, box.Id);
            Assert.Equal(42, box.Number);
            Assert.Equal(7, box.AreaCode);
            Assert.Equal(new DateTime(2024, 2, 3, 4, 5, 6), box.CreatedAt);
            Assert.Equal("Historical category", (await store.Connection.Table<Category>().FirstAsync()).Name);
            Assert.Equal(8, (await store.Connection.Table<BoxCat>().FirstAsync()).Id);
            Assert.Equal("Storage", (await store.Connection.Table<Area>().FirstAsync()).Name);
            await store.Connection.InsertAsync(new Box { Number = 43, Description = "After restore" });
        }
        Assert.Empty(Directory.GetFiles(directory, "*.restore-*"));
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("text")]
    [InlineData("unrelated")]
    [InlineData("missing-columns")]
    [InlineData("duplicate-numbers")]
    [InlineData("invalid-date")]
    public async Task InvalidRestoreDoesNotChangeDataAndConnectionRemainsWritable(string kind)
    {
        using (await store.AcquireAsync())
            await store.Connection.InsertAsync(new Box { Number = 1, Description = "Keep me" });

        byte[] bytes;
        if (kind == "empty") bytes = [];
        else if (kind == "text") bytes = "Not a database"u8.ToArray();
        else
        {
            var candidate = PathFor("invalid.db3");
            using (var db = new SQLiteConnection(candidate))
            {
                if (kind == "unrelated") db.Execute("CREATE TABLE Other (Id INTEGER)");
                else if (kind == "missing-columns") db.Execute("CREATE TABLE Boxes (Id INTEGER)");
                else if (kind == "invalid-date")
                {
                    db.Execute("CREATE TABLE Boxes (Id INTEGER PRIMARY KEY, Number INTEGER, Description TEXT, CreatedAt INTEGER)");
                    db.Execute("INSERT INTO Boxes VALUES (1, 1, 'Invalid date', ?)", long.MaxValue);
                }
                else
                {
                    db.Execute("CREATE TABLE Boxes (Id INTEGER PRIMARY KEY, Number INTEGER, Description TEXT)");
                    db.Execute("INSERT INTO Boxes VALUES (1, 1, 'First'), (2, 1, 'Duplicate')");
                }
            }
            bytes = await File.ReadAllBytesAsync(candidate);
        }
        await using var source = new MemoryStream(bytes);
        await Assert.ThrowsAnyAsync<Exception>(() => store.RestoreAsync(source));
        Assert.True(store.IsInitialized);
        using (await store.AcquireAsync())
        {
            Assert.Equal("Keep me", (await store.Connection.Table<Box>().FirstAsync()).Description);
            await store.Connection.InsertAsync(new Box { Number = 2, Description = "Still writable" });
        }
        Assert.Empty(Directory.GetFiles(directory, "*.restore-*"));
        Assert.False(Directory.Exists(PathFor("DatabaseRecovery")));
    }

    [Fact]
    public async Task CancellationAndInterruptedStreamLeaveDatabaseUntouched()
    {
        using (await store.AcquireAsync())
            await store.Connection.InsertAsync(new Box { Number = 1, Description = "Keep me" });

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await using var cancelledSource = new MemoryStream(new byte[100]);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.RestoreAsync(cancelledSource, cancellation.Token));
        await using var brokenSource = new InterruptedStream();
        await Assert.ThrowsAsync<IOException>(() => store.RestoreAsync(brokenSource));
        using (await store.AcquireAsync())
            Assert.Equal("Keep me", (await store.Connection.Table<Box>().FirstAsync()).Description);
        Assert.Empty(Directory.GetFiles(directory, "*.restore-*"));
    }

    [Fact]
    public async Task FileReplacementFailureReopensOriginalAndKeepsRecoveryCopy()
    {
        if (!OperatingSystem.IsWindows()) return;
        using (await store.AcquireAsync())
            await store.Connection.InsertAsync(new Box { Number = 1, Description = "Original" });
        var snapshot = PathFor("incoming.db3");
        await store.BackupAsync(snapshot);
        await using var source = File.OpenRead(snapshot);
        using var lockedFile = new FileStream(PathFor("BigBazar.db3"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var failure = await Record.ExceptionAsync(() => store.RestoreAsync(source));
        Assert.True(failure is IOException or UnauthorizedAccessException);
        Assert.True(store.IsInitialized);
        using (await store.AcquireAsync())
        {
            Assert.Equal("Original", (await store.Connection.Table<Box>().FirstAsync()).Description);
            await store.Connection.InsertAsync(new Box { Number = 2, Description = "Writable after failure" });
        }
        Assert.Single(Directory.GetFiles(PathFor("DatabaseRecovery"), "*.db3"));
    }

    [Fact]
    public async Task ActiveLeaseExcludesBackupRestoreResetAndOtherReaders()
    {
        var incoming = PathFor("incoming.db3");
        using (var db = new SQLiteConnection(incoming))
        {
            db.CreateTable<Box>();
            db.Insert(new Box { Number = 42, Description = "Incoming" });
        }
        await using var source = File.OpenRead(incoming);
        var lease = await store.AcquireAsync();
        var reader = store.AcquireAsync();
        var backup = store.BackupAsync(PathFor("backup.db3"));
        var restore = store.RestoreAsync(source);
        Assert.False(reader.IsCompleted);
        Assert.False(backup.IsCompleted);
        Assert.False(restore.IsCompleted);
        lease.Dispose();
        using (await reader) { }
        await Task.WhenAll(backup, restore).WaitAsync(TimeSpan.FromSeconds(15));
        using (await store.AcquireAsync())
            Assert.Equal(42, (await store.Connection.Table<Box>().FirstAsync()).Number);

        lease = await store.AcquireAsync();
        var reset = store.ResetAsync();
        Assert.False(reset.IsCompleted);
        lease.Dispose();
        await reset.WaitAsync(TimeSpan.FromSeconds(15));
        using (await store.AcquireAsync())
            Assert.Empty(await store.Connection.Table<Box>().ToListAsync());
    }

    [Fact]
    public async Task FailedInitializationCanBeRetriedAfterRepair()
    {
        await File.WriteAllTextAsync(PathFor("BigBazar.db3"), "Not SQLite");
        await Assert.ThrowsAnyAsync<Exception>(() => store.InitializeAsync());
        Assert.False(store.IsInitialized);
        File.Delete(PathFor("BigBazar.db3"));
        await store.InitializeAsync();
        Assert.True(store.IsInitialized);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(72)]
    [InlineData(71)]
    [InlineData(30)]
    public async Task HistoricalStreamRestoreAndBackupPreserveOriginalValues(int count)
    {
        var incomingPath = PathFor("historical.db3");
        var referenceDirectory = Environment.GetEnvironmentVariable("SMALLBAZAR_REFERENCE_DIRECTORY");
        if (count > 0 && !string.IsNullOrWhiteSpace(referenceDirectory))
        {
            var label = count switch
            {
                72 => "SmallBazar-installed",
                71 => "SmallBazar-existing-backup",
                _ => "BigBazar-previous"
            };
            File.Copy(Directory.GetFiles(Path.Combine(referenceDirectory, label), "*.db3").Single(), incomingPath);
        }
        else
        {
            using var db = new SQLiteConnection(incomingPath);
            db.CreateTable<Category>();
            db.CreateTable<BoxCat>();
            if (count == 30)
                db.Execute("CREATE TABLE Boxes (Id INTEGER PRIMARY KEY, Number INTEGER UNIQUE, Description TEXT)");
            else
            {
                db.CreateTable<Box>();
                db.CreateTable<Area>();
                if (count > 0) db.Insert(new Area { Name = "Test storage" });
            }
            for (var number = 1; number <= count; number++)
                db.Execute("INSERT INTO Boxes (Id, Number, Description) VALUES (?, ?, ?)", number, number, $"Historical box {number}");
        }

        var incoming = new SQLiteAsyncConnection(incomingPath);
        var snapshots = new Dictionary<string, (string[] Columns, string[] Rows)>();
        try
        {
            foreach (var table in new[] { "Boxes", "Areas", "Categories", "BoxCats" })
            {
                var columns = (await incoming.GetTableInfoAsync(table)).Select(c => c.Name).ToArray();
                if (columns.Length > 0)
                    snapshots.Add(table, (columns, await DatabaseSchemaTests.SnapshotAsync(incoming, table, columns)));
            }
        }
        finally
        {
            await incoming.CloseAsync();
        }

        await using (var source = File.OpenRead(incomingPath))
            await store.RestoreAsync(source);
        var snapshotPath = PathFor("roundtrip.db3");
        await store.BackupAsync(snapshotPath);
        await using (var source = File.OpenRead(snapshotPath))
            await store.RestoreAsync(source);
        using (await store.AcquireAsync())
        {
            foreach (var (table, snapshot) in snapshots)
                Assert.Equal(snapshot.Rows, await DatabaseSchemaTests.SnapshotAsync(store.Connection, table, snapshot.Columns));
            Assert.Equal(count, await store.Connection.Table<Box>().CountAsync());
            if (!snapshots.ContainsKey("Areas")) Assert.Empty(await store.Connection.Table<Area>().ToListAsync());
            Assert.Equal("ok", await store.Connection.ExecuteScalarAsync<string>("PRAGMA integrity_check"));
        }
        Assert.Equal(2, Directory.GetFiles(PathFor("DatabaseRecovery"), "*.db3").Length);
    }

    private sealed class NonSeekableStream(byte[] contents) : MemoryStream(contents)
    {
        public override bool CanSeek => false;
        public override long Seek(long offset, SeekOrigin loc) => throw new NotSupportedException();
        public override long Length => throw new NotSupportedException();
    }

    private sealed class InterruptedStream : MemoryStream
    {
        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken) =>
            Task.FromException(new IOException("The provider interrupted the read."));
    }
}
