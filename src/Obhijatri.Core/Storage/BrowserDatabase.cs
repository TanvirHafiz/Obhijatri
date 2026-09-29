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

    /// <summary>An in-memory database, gone when disposed. Used by tests.</summary>
    public static BrowserDatabase OpenInMemory() => Create("Data Source=:memory:", useWal: false);

    private static BrowserDatabase Create(string connectionString, bool useWal)
    {
        var connection = new SqliteConnection(connectionString);
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
