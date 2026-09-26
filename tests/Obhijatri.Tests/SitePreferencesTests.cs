using Microsoft.Data.Sqlite;
using Obhijatri.Core.Settings;
using Obhijatri.Core.Storage;

namespace Obhijatri.Tests;

public sealed class SitePreferencesTests
{
    [Fact]
    public void Phonetic_IsOffByDefault_AndOnlyOnIsStored()
    {
        using var db = BrowserDatabase.OpenInMemory();
        var prefs = new SitePreferencesStore(db);

        Assert.False(prefs.GetPhonetic("www.prothomalo.com"));

        prefs.SetPhonetic("www.prothomalo.com", true);
        prefs.SetPhonetic("www.facebook.com", true);
        prefs.SetPhonetic("www.facebook.com", false);

        Assert.True(prefs.GetPhonetic("WWW.ProthomAlo.com."));
        Assert.False(prefs.GetPhonetic("www.facebook.com"));
        Assert.Equal(1, prefs.Count());

        Assert.Equal(1, prefs.ClearAll());
        Assert.False(prefs.GetPhonetic("www.prothomalo.com"));
    }

    [Fact]
    public void AdsAllowed_AndPhonetic_ShareOneRow_RemovedWhenBothOff()
    {
        using var db = BrowserDatabase.OpenInMemory();
        var prefs = new SitePreferencesStore(db);

        prefs.SetAdsAllowed("www.jugantor.com", true);
        prefs.SetPhonetic("www.jugantor.com", true);
        Assert.True(prefs.GetAdsAllowed("www.jugantor.com"));
        Assert.Equal(1, prefs.Count());

        prefs.SetAdsAllowed("www.jugantor.com", false);
        Assert.False(prefs.GetAdsAllowed("www.jugantor.com"));
        Assert.True(prefs.GetPhonetic("www.jugantor.com"));
        Assert.Equal(1, prefs.Count());

        prefs.SetPhonetic("www.jugantor.com", false);
        Assert.Equal(0, prefs.Count());
    }

    [Fact]
    public void EmptyHost_IsIgnored()
    {
        using var db = BrowserDatabase.OpenInMemory();
        var prefs = new SitePreferencesStore(db);
        prefs.SetPhonetic("  ", true);
        Assert.Equal(0, prefs.Count());
    }

    [Fact]
    public void AddressBarPhonetic_DefaultsOff()
    {
        using var db = BrowserDatabase.OpenInMemory();
        var settings = new BrowserSettings(new SettingsStore(db));
        Assert.False(settings.AddressBarPhonetic);
        settings.AddressBarPhonetic = true;
        Assert.True(settings.AddressBarPhonetic);
    }

    [Fact]
    public void Version1Database_IsUpgraded_KeepingData()
    {
        var path = Path.Combine(Path.GetTempPath(), $"obhijatri-v1-{Guid.NewGuid():N}.db");
        try
        {
            // A database as Milestone 2 and 3 created it: schema version 1, no site_preferences table.
            using (var connection = new SqliteConnection($"Data Source={path};Pooling=False"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = """
                    CREATE TABLE history (id INTEGER PRIMARY KEY, url TEXT NOT NULL, title TEXT NOT NULL DEFAULT '', visited_at INTEGER NOT NULL);
                    CREATE TABLE bookmarks (id INTEGER PRIMARY KEY, parent_id INTEGER NULL REFERENCES bookmarks(id) ON DELETE CASCADE, is_folder INTEGER NOT NULL, title TEXT NOT NULL, url TEXT NULL, position INTEGER NOT NULL, created_at INTEGER NOT NULL);
                    CREATE TABLE settings (key TEXT PRIMARY KEY, value TEXT NOT NULL);
                    CREATE TABLE session_tabs (position INTEGER PRIMARY KEY, url TEXT NOT NULL, title TEXT NOT NULL, is_active INTEGER NOT NULL);
                    INSERT INTO history (url, title, visited_at) VALUES ('https://www.prothomalo.com/', 'প্রথম আলো', 1);
                    PRAGMA user_version = 1;
                    """;
                command.ExecuteNonQuery();
            }

            using var db = BrowserDatabase.Open(path);
            Assert.Equal("প্রথম আলো", Assert.Single(new HistoryStore(db).Search(null)).Title);
            var prefs = new SitePreferencesStore(db);
            prefs.SetPhonetic("www.prothomalo.com", true);
            prefs.SetAdsAllowed("www.prothomalo.com", true);
            Assert.True(prefs.GetPhonetic("www.prothomalo.com"));
            Assert.True(prefs.GetAdsAllowed("www.prothomalo.com"));
        }
        finally
        {
            foreach (var file in new[] { path, path + "-wal", path + "-shm" })
            {
                File.Delete(file);
            }
        }
    }
}
