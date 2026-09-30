using Microsoft.Data.Sqlite;

namespace Obhijatri.Core.Storage;

/// <summary>
/// The single SQLite database holding history, bookmarks, settings and the saved session.
/// One connection, used from the UI thread only. SQLite calls here are small and fast.
/// </summary>
public sealed class BrowserDatabase : IDisposable
{
    private const int SchemaVersion = 5;

    private BrowserDatabase(SqliteConnection connection)
    {
        Connection = connection;
    }

    internal SqliteConnection Connection { get; }

    /// <summary>Opens (and creates if needed) the database file at <paramref name="path"/>.</summary>
    public static BrowserDatabase Open(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Private,
            Pooling = false,
        };
        return Create(builder.ToString(), useWal: true);
    }

    /// <summary>How many damaged copies are kept next to the database.</summary>
    public const int MaxBackups = 3;

    /// <summary>
    /// Opens the database, and if the file is damaged (not a database, or its pages are corrupt) moves it
    /// aside as <c>obhijatri.db.corrupt-YYYYMMDD-HHMMSS</c> and starts a fresh one, so that a bad file never
    /// stops the browser from starting. <paramref name="backupPath"/> is the moved file, or null when the
    /// database was healthy. Only the newest <see cref="MaxBackups"/> damaged copies are kept.
    /// </summary>
    /// <exception cref="IOException">The damaged file could not be moved aside (for example it is locked).</exception>
    public static BrowserDatabase OpenOrRecover(string path, out string? backupPath, TimeProvider? time = null)
    {
        backupPath = null;
        try
        {
            var db = Open(path);
            if (db.IsHealthy())
            {
                return db;
            }
            db.Dispose();
        }
        catch (SqliteException ex) when (IsDamage(ex))
        {
            // Not a database, or corrupt from the first page: handled below.
        }

        backupPath = MoveAside(path, time ?? TimeProvider.System);
        return Open(path);
    }

    /// <summary>The database's own quick consistency check (reads the file's structure, not every row).</summary>
    private bool IsHealthy()
    {
        try
        {
            using var command = Command("PRAGMA quick_check(1);");
            return string.Equals(command.ExecuteScalar() as string, "ok", StringComparison.OrdinalIgnoreCase);
        }
        catch (SqliteException ex) when (IsDamage(ex))
        {
            return false;
        }
    }

    // SQLITE_CORRUPT (11) and SQLITE_NOTADB (26). Other errors (a locked file, a full disk) are not damage.
    private static bool IsDamage(SqliteException ex) => ex.SqliteErrorCode is 11 or 26;

    private static string MoveAside(string path, TimeProvider time)
    {
        var stamp = time.GetLocalNow().ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
        var backup = path + ".corrupt-" + stamp;
        for (var n = 2; File.Exists(backup); n++)
        {
            backup = path + ".corrupt-" + stamp + "-" + n.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        File.Move(path, backup);
        // The write-ahead log and shared memory files belong to the damaged database, not the new one.
        foreach (var suffix in new[] { "-wal", "-shm" })
        {
            if (File.Exists(path + suffix))
            {
                File.Move(path + suffix, backup + suffix);
            }
        }

        var folder = Path.GetDirectoryName(path)!;
        var name = Path.GetFileName(path);
        var old = Directory.GetFiles(folder, name + ".corrupt-*")
            .Where(f => !f.EndsWith("-wal", StringComparison.Ordinal) && !f.EndsWith("-shm", StringComparison.Ordinal))
            .OrderByDescending(f => File.GetLastWriteTimeUtc(f))
            .Skip(MaxBackups)
            .ToList();
        foreach (var file in old)
        {
            foreach (var suffix in new[] { string.Empty, "-wal", "-shm" })
            {
                try
                {
                    File.Delete(file + suffix);
                }
                catch (IOException)
                {
                    // An old backup that cannot be deleted now is not worth failing over.
                }
            }
        }
        return backup;
    }

    /// <summary>An in-memory database, gone when disposed. Used by tests.</summary>
    public static BrowserDatabase OpenInMemory() => Create("Data Source=:memory:", useWal: false);

    private static BrowserDatabase Create(string connectionString, bool useWal)
    {
        var connection = new SqliteConnection(connectionString);
        try
        {
            connection.Open();
            var db = new BrowserDatabase(connection);
            db.Execute("PRAGMA foreign_keys = ON;");
            if (useWal)
            {
                db.Execute("PRAGMA journal_mode = WAL;");
                db.Execute("PRAGMA synchronous = NORMAL;");
            }
            db.Migrate();
            return db;
        }
        catch
        {
            // A file SQLite refuses must not stay locked by a half-opened connection: it may need moving aside.
            connection.Dispose();
            throw;
        }
    }

    private void Migrate()
    {
        var version = Convert.ToInt32(Scalar("PRAGMA user_version;"), System.Globalization.CultureInfo.InvariantCulture);
        if (version >= SchemaVersion)
        {
            return;
        }

        using var transaction = Connection.BeginTransaction();
        if (version < 1)
        {
            CreateVersion1(transaction);
        }
        if (version < 2)
        {
            // Sites where the user switched Bangla phonetic typing on. Only "on" is stored.
            Execute("""
                CREATE TABLE IF NOT EXISTS site_preferences (
                    host     TEXT PRIMARY KEY,
                    phonetic INTEGER NOT NULL
                );
                """, transaction);
        }
        if (version < 3)
        {
            // Sites where the user chose to see ads. Like phonetic, only "on" is kept.
            Execute("ALTER TABLE site_preferences ADD COLUMN ads_allowed INTEGER NOT NULL DEFAULT 0;", transaction); // not-ui
        }
        if (version < 4)
        {
            // Per-site camera/microphone/location/notification choices (0 = ask, 1 = allow, 2 =
            // deny). A row exists only while at least one is away from "ask".
            Execute("""
                CREATE TABLE IF NOT EXISTS site_permissions (
                    host          TEXT PRIMARY KEY,
                    camera        INTEGER NOT NULL DEFAULT 0,
                    microphone    INTEGER NOT NULL DEFAULT 0,
                    location      INTEGER NOT NULL DEFAULT 0,
                    notifications INTEGER NOT NULL DEFAULT 0
                );
                """, transaction);
        }
        if (version < 5)
        {
            // Pinned tabs stay awake (Milestone 8) and keep their place at the start of the strip.
            Execute("ALTER TABLE session_tabs ADD COLUMN is_pinned INTEGER NOT NULL DEFAULT 0;", transaction); // not-ui
        }
        Execute($"PRAGMA user_version = {SchemaVersion};", transaction);
        transaction.Commit();
    }

    private void CreateVersion1(SqliteTransaction transaction)
    {
        Execute("""
            CREATE TABLE IF NOT EXISTS history (
                id         INTEGER PRIMARY KEY,
                url        TEXT    NOT NULL,
                title      TEXT    NOT NULL DEFAULT '',
                visited_at INTEGER NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_history_visited_at ON history(visited_at);
            CREATE INDEX IF NOT EXISTS ix_history_url ON history(url);

            CREATE TABLE IF NOT EXISTS bookmarks (
                id         INTEGER PRIMARY KEY,
                parent_id  INTEGER NULL REFERENCES bookmarks(id) ON DELETE CASCADE,
                is_folder  INTEGER NOT NULL,
                title      TEXT    NOT NULL,
                url        TEXT    NULL,
                position   INTEGER NOT NULL,
                created_at INTEGER NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_bookmarks_parent ON bookmarks(parent_id, position);
            CREATE INDEX IF NOT EXISTS ix_bookmarks_url ON bookmarks(url);

            CREATE TABLE IF NOT EXISTS settings (
                key   TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS session_tabs (
                position  INTEGER PRIMARY KEY,
                url       TEXT    NOT NULL,
                title     TEXT    NOT NULL,
                is_active INTEGER NOT NULL
            );
            """, transaction);
    }

    internal SqliteCommand Command(string sql, SqliteTransaction? transaction = null)
    {
        var command = Connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction;
        return command;
    }

    internal int Execute(string sql, SqliteTransaction? transaction = null)
    {
        using var command = Command(sql, transaction);
        return command.ExecuteNonQuery();
    }

    internal object? Scalar(string sql)
    {
        using var command = Command(sql);
        return command.ExecuteScalar();
    }

    public void Dispose()
    {
        Connection.Dispose();
    }
}
