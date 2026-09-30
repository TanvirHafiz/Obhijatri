using Microsoft.Data.Sqlite;
using Obhijatri.Core.Storage;

namespace Obhijatri.Tests;

public sealed class DatabaseRecoveryTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "obhijatri-recovery-" + Guid.NewGuid().ToString("N"));
    private readonly string _path;

    public DatabaseRecoveryTests()
    {
        Directory.CreateDirectory(_folder);
        _path = Path.Combine(_folder, "obhijatri.db");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (IOException)
        {
            // A temp folder that is still in use is left for the system to clean.
        }
    }

    private static void Fill(string path, int rows)
    {
        using var db = BrowserDatabase.Open(path);
        var history = new HistoryStore(db);
        for (var i = 0; i < rows; i++)
        {
            history.AddVisit("https://example.com/page/" + i, "Page number " + i + " with a longish title to fill pages");
        }
        new SettingsStore(db).SetString("ui.language", "bn-BD");
    }

    private static void AssertUsable(BrowserDatabase db)
    {
        var settings = new SettingsStore(db);
        settings.SetString("test.key", "value");
        Assert.Equal("value", settings.GetString("test.key"));
        new HistoryStore(db).AddVisit("https://after.example/", "after");
        Assert.Single(new HistoryStore(db).Search("after.example"));
    }

    [Fact]
    public void HealthyDatabase_IsOpenedAsIs_WithNoBackup()
    {
        Fill(_path, 50);

        using var db = BrowserDatabase.OpenOrRecover(_path, out var backup);

        Assert.Null(backup);
        Assert.Equal("bn-BD", new SettingsStore(db).GetString("ui.language"));
        Assert.Equal(50, new HistoryStore(db).Count());
    }

    [Fact]
    public void HealthCheck_OnALargeHistory_IsFastEnoughForEveryStart()
    {
        using (var db = BrowserDatabase.Open(_path))
        {
            var history = new HistoryStore(db);
            db.Connection.CreateCommand().ExecuteNonQuery(); // keeps the connection warm
            using var transaction = db.Connection.BeginTransaction();
            for (var i = 0; i < 100_000; i++)
            {
                using var command = db.Connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = "INSERT INTO history (url, title, visited_at) VALUES ($u, $t, $a);";
                command.Parameters.AddWithValue("$u", "https://www.example.com/news/2026/09/30/story-number-" + i);
                command.Parameters.AddWithValue("$t", "A typical news headline for story number " + i);
                command.Parameters.AddWithValue("$a", 1_700_000_000_000L + i);
                command.ExecuteNonQuery();
            }
            transaction.Commit();
            Assert.Equal(100_000, history.Count());
        }
        SqliteConnection.ClearAllPools();
        var megabytes = new FileInfo(_path).Length / 1048576.0;

        var watch = System.Diagnostics.Stopwatch.StartNew();
        using (var db = BrowserDatabase.OpenOrRecover(_path, out var backup))
        {
            watch.Stop();
            Assert.Null(backup);
        }

        Assert.True(watch.ElapsedMilliseconds < 400, $"opening a {megabytes:F1} MB history took {watch.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void MissingDatabase_IsCreated_WithNoBackup()
    {
        using var db = BrowserDatabase.OpenOrRecover(_path, out var backup);

        Assert.Null(backup);
        Assert.True(File.Exists(_path));
        AssertUsable(db);
    }

    [Fact]
    public void GarbageFile_IsMovedAside_AndAFreshDatabaseStarts()
    {
        var garbage = new byte[4096];
        new Random(1).NextBytes(garbage);
        File.WriteAllBytes(_path, garbage);

        using var db = BrowserDatabase.OpenOrRecover(_path, out var backup);

        Assert.NotNull(backup);
        Assert.Contains(".corrupt-", backup, StringComparison.Ordinal);
        Assert.Equal(garbage, File.ReadAllBytes(backup)); // kept untouched for the person
        AssertUsable(db);
        Assert.Equal(0, new HistoryStore(db).Count() - 1);
    }

    [Fact]
    public void TextFileInPlaceOfTheDatabase_IsRecovered()
    {
        File.WriteAllText(_path, "this is not a database, someone saved over it");

        using var db = BrowserDatabase.OpenOrRecover(_path, out var backup);

        Assert.NotNull(backup);
        AssertUsable(db);
    }

    [Fact]
    public void EmptyFile_IsAcceptedAsANewDatabase()
    {
        File.WriteAllBytes(_path, []);

        using var db = BrowserDatabase.OpenOrRecover(_path, out var backup);

        Assert.Null(backup); // an empty file is a valid empty SQLite database
        AssertUsable(db);
    }

    [Fact]
    public void TruncatedDatabase_IsRecovered()
    {
        Fill(_path, 3000);
        SqliteConnection.ClearAllPools();
        var length = new FileInfo(_path).Length;
        using (var stream = new FileStream(_path, FileMode.Open, FileAccess.Write))
        {
            stream.SetLength(length / 2);
        }
        // The log of the last session could hide the damage; a real crash may leave it, so remove it.
        foreach (var suffix in new[] { "-wal", "-shm" })
        {
            File.Delete(_path + suffix);
        }

        using var db = BrowserDatabase.OpenOrRecover(_path, out var backup);

        Assert.NotNull(backup);
        Assert.True(File.Exists(backup));
        AssertUsable(db);
    }

    [Fact]
    public void DatabaseWithDamagedPages_IsRecovered()
    {
        Fill(_path, 3000);
        SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { "-wal", "-shm" })
        {
            File.Delete(_path + suffix);
        }

        // Overwrite a stretch in the middle of the file (page contents, not the header).
        var bytes = File.ReadAllBytes(_path);
        for (var i = bytes.Length / 3; i < bytes.Length / 3 + 3000; i++)
        {
            bytes[i] = 0xFF;
        }
        File.WriteAllBytes(_path, bytes);

        using var db = BrowserDatabase.OpenOrRecover(_path, out var backup);

        Assert.NotNull(backup);
        AssertUsable(db);
    }

    [Fact]
    public void StaleLogFiles_MoveWithTheDamagedDatabase()
    {
        File.WriteAllText(_path, "garbage database");
        File.WriteAllText(_path + "-wal", "garbage log");
        File.WriteAllText(_path + "-shm", "garbage shared memory");

        using var db = BrowserDatabase.OpenOrRecover(_path, out var backup);

        Assert.NotNull(backup);

        // SQLite may discard an invalid log itself while opening; if a log or shared memory file was left, it
        // must have moved with the database and not stay beside the new one.
        if (File.Exists(backup + "-wal"))
        {
            Assert.Equal("garbage log", File.ReadAllText(backup + "-wal"));
        }
        AssertUsable(db);
        foreach (var (suffix, old) in new[] { ("-wal", "garbage log"), ("-shm", "garbage shared memory") })
        {
            if (File.Exists(_path + suffix))
            {
                // The new database's own live files: read them while it has them open.
                using var stream = new FileStream(_path + suffix, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                Assert.DoesNotContain(old, reader.ReadToEnd(), StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void OnlyTheNewestBackupsAreKept()
    {
        var start = new DateTime(2026, 9, 30, 10, 0, 0, DateTimeKind.Utc);
        var backups = new List<string>();
        for (var i = 0; i < BrowserDatabase.MaxBackups + 3; i++)
        {
            File.WriteAllText(_path, "garbage number " + i);
            File.SetLastWriteTimeUtc(_path, start.AddMinutes(i));
            using var db = BrowserDatabase.OpenOrRecover(_path, out var backup, new ManualTimeProvider(new DateTimeOffset(start.AddMinutes(i))));
            backups.Add(backup!);
            SqliteConnection.ClearAllPools();
        }

        var left = Directory.GetFiles(_folder, "obhijatri.db.corrupt-*").Where(f => !f.EndsWith("-wal") && !f.EndsWith("-shm")).ToList();
        Assert.Equal(BrowserDatabase.MaxBackups, left.Count);
        Assert.All(backups.Skip(3), b => Assert.Contains(b, left)); // the newest three survive
        Assert.All(backups.Take(3), b => Assert.DoesNotContain(b, left));
    }

    [Fact]
    public void TwoRecoveriesInTheSameSecond_DoNotOverwriteEachOther()
    {
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 9, 30, 10, 0, 0, TimeSpan.Zero));
        File.WriteAllText(_path, "first garbage");
        using (BrowserDatabase.OpenOrRecover(_path, out var first, time))
        {
            Assert.NotNull(first);
        }
        SqliteConnection.ClearAllPools();

        File.Delete(_path);
        File.WriteAllText(_path, "second garbage");
        using var db = BrowserDatabase.OpenOrRecover(_path, out var second, time);

        Assert.NotNull(second);
        Assert.Equal(2, Directory.GetFiles(_folder, "obhijatri.db.corrupt-*").Length);
    }

    [Fact]
    public void ARealFailureThatIsNotDamage_IsNotTreatedAsCorruption()
    {
        // A directory where the file should be: SQLite cannot open it, but nothing is corrupt, so nothing is moved.
        Directory.CreateDirectory(_path);

        Assert.ThrowsAny<Exception>(() => BrowserDatabase.OpenOrRecover(_path, out _));

        Assert.True(Directory.Exists(_path));
        Assert.Empty(Directory.GetFiles(_folder, "*.corrupt-*"));
    }

    [Fact]
    public void SettingsAreRecoveredToTheirDefaults_AfterTheDatabaseIsReplaced()
    {
        Fill(_path, 10);
        SqliteConnection.ClearAllPools();
        File.WriteAllText(_path, "overwritten");
        foreach (var suffix in new[] { "-wal", "-shm" })
        {
            File.Delete(_path + suffix);
        }

        using var db = BrowserDatabase.OpenOrRecover(_path, out _);
        var settings = new Obhijatri.Core.Settings.BrowserSettings(new SettingsStore(db));

        Assert.Equal(Obhijatri.Core.Settings.BrowserSettings.Bangla, settings.UiLanguage); // the default
        Assert.True(settings.BlockAds);
        Assert.True(settings.HttpsOnly);
    }
}
