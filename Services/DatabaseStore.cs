using SQLite;

namespace SmallBazar.Services;

/// <summary>
/// Serializes database access and protects replacement with a validated snapshot and recovery copy.
/// </summary>
internal sealed class DatabaseStore : IAsyncDisposable
{
    internal const SQLiteOpenFlags OpenFlags = SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex;
    private readonly string path;
    private readonly SemaphoreSlim gate = new(1, 1);
    private SQLiteAsyncConnection connection;

    internal DatabaseStore(string path) => this.path = Path.GetFullPath(path);

    internal bool IsInitialized { get; private set; }

    /// <summary>
    /// Gets the connection while the caller owns an access lease.
    /// </summary>
    internal SQLiteAsyncConnection Connection => connection;

    /// <summary>
    /// Acquires exclusive access and initializes the schema before exposing the connection.
    /// </summary>
    internal async Task<IDisposable> AcquireAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await InitializeCoreAsync().ConfigureAwait(false);
            return new Lease(gate);
        }
        catch
        {
            gate.Release();
            throw;
        }
    }

    internal async Task InitializeAsync()
    {
        using var lease = await AcquireAsync().ConfigureAwait(false);
    }

    private async Task InitializeCoreAsync()
    {
        if (IsInitialized) return;
        connection ??= new SQLiteAsyncConnection(path, OpenFlags);
        try
        {
            await DatabaseSchema.InitializeAsync(connection).ConfigureAwait(false);
            IsInitialized = true;
        }
        catch
        {
            await CloseCoreAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Creates a consistent backup, including committed data still in the WAL, without closing the live database.
    /// </summary>
    internal async Task BackupAsync(string destination, CancellationToken cancellationToken = default)
    {
        destination = Path.GetFullPath(destination);
        if (string.Equals(destination, path, StringComparison.OrdinalIgnoreCase))
            throw new IOException("The backup cannot replace the active database.");
        using var lease = await AcquireAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (File.Exists(destination)) throw new IOException("The backup file already exists.");
        await connection.BackupAsync(destination).ConfigureAwait(false);
        await ValidateAsync(destination).ConfigureAwait(false);
    }

    /// <summary>
    /// Stages a stream, validates its historical schema, then replaces the database with rollback on failure.
    /// </summary>
    internal async Task<string> RestoreAsync(Stream source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        var stagedPath = path + $".restore-{Guid.NewGuid():N}.db3";
        try
        {
            await using (var staged = new FileStream(stagedPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                81920, FileOptions.Asynchronous))
            {
                await source.CopyToAsync(staged, cancellationToken).ConfigureAwait(false);
                await staged.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            await ValidateAsync(stagedPath).ConfigureAwait(false);
            var candidate = new SQLiteAsyncConnection(stagedPath, OpenFlags);
            try
            {
                await DatabaseSchema.InitializeAsync(candidate).ConfigureAwait(false);
                await candidate.Table<Models.Box>().ToListAsync().ConfigureAwait(false);
                await candidate.Table<Models.Area>().ToListAsync().ConfigureAwait(false);
                await candidate.Table<Models.Category>().ToListAsync().ConfigureAwait(false);
                await candidate.Table<Models.BoxCat>().ToListAsync().ConfigureAwait(false);
            }
            finally
            {
                await candidate.CloseAsync().ConfigureAwait(false);
            }
            await ValidateAsync(stagedPath).ConfigureAwait(false);

            using var lease = await AcquireAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var recoveryDirectory = Path.Combine(Path.GetDirectoryName(path), "DatabaseRecovery");
            Directory.CreateDirectory(recoveryDirectory);
            var recoveryPath = Path.Combine(recoveryDirectory, $"before-restore-{DateTime.UtcNow:yyyyMMddTHHmmssfffffffZ}-{Guid.NewGuid():N}.db3");
            await connection.BackupAsync(recoveryPath).ConfigureAwait(false);
            await ValidateAsync(recoveryPath).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            await CloseCoreAsync().ConfigureAwait(false);
            var replaced = false;
            try
            {
                File.Move(stagedPath, path, true);
                replaced = true;
                await InitializeCoreAsync().ConfigureAwait(false);
                return recoveryPath;
            }
            catch (Exception restoreError)
            {
                try
                {
                    await CloseCoreAsync().ConfigureAwait(false);
                    if (replaced) File.Copy(recoveryPath, path, true);
                    await InitializeCoreAsync().ConfigureAwait(false);
                }
                catch (Exception recoveryError)
                {
                    throw new AggregateException($"Restoration and recovery failed. The recovery database is at {recoveryPath}.",
                        restoreError, recoveryError);
                }
                throw;
            }
        }
        finally
        {
            if (File.Exists(stagedPath)) File.Delete(stagedPath);
        }
    }

    /// <summary>
    /// Rejects corrupt files and unrelated SQLite databases before they can replace application data.
    /// </summary>
    internal static async Task ValidateAsync(string databasePath)
    {
        if (new FileInfo(databasePath).Length == 0) throw new InvalidDataException("The selected database is empty.");
        var candidate = new SQLiteAsyncConnection(databasePath, SQLiteOpenFlags.ReadOnly | SQLiteOpenFlags.FullMutex);
        try
        {
            var integrity = await candidate.QueryScalarsAsync<string>("PRAGMA integrity_check").ConfigureAwait(false);
            if (integrity.Count != 1 || integrity[0] != "ok")
                throw new InvalidDataException("The selected database failed its integrity check.");
            var columns = await candidate.GetTableInfoAsync("Boxes").ConfigureAwait(false);
            if (!new[] { "Id", "Number", "Description" }.All(required =>
                columns.Any(column => string.Equals(column.Name, required, StringComparison.OrdinalIgnoreCase))))
                throw new InvalidDataException("The selected file is not a compatible SmallBazar database.");
        }
        finally
        {
            await candidate.CloseAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Recreates an empty database under the same exclusive access gate.
    /// </summary>
    internal async Task ResetAsync()
    {
        using var lease = await AcquireAsync().ConfigureAwait(false);
        await CloseCoreAsync().ConfigureAwait(false);
        File.Delete(path);
        await InitializeCoreAsync().ConfigureAwait(false);
    }

    private async Task CloseCoreAsync()
    {
        IsInitialized = false;
        if (connection == null) return;
        await connection.CloseAsync().ConfigureAwait(false);
        connection = null;
    }

    public async ValueTask DisposeAsync()
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await CloseCoreAsync().ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    private sealed class Lease(SemaphoreSlim gate) : IDisposable
    {
        private SemaphoreSlim owner = gate;

        public void Dispose() => Interlocked.Exchange(ref owner, null)?.Release();
    }
}
