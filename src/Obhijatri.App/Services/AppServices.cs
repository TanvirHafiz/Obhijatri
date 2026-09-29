using Microsoft.Data.Sqlite;
using Obhijatri.App.Downloads;
using Obhijatri.Core;
using Obhijatri.Core.Performance;
using Obhijatri.Core.Settings;
using Obhijatri.Core.Storage;

namespace Obhijatri.App.Services;

/// <summary>App-wide stores. All of them are used from the UI thread only.</summary>
internal static class AppServices
{
    public static BrowserDatabase Database { get; private set; } = null!;
    public static HistoryStore History { get; private set; } = null!;
    public static BookmarkStore Bookmarks { get; private set; } = null!;
    public static BrowserSettings Settings { get; private set; } = null!;
    public static SessionStore Sessions { get; private set; } = null!;
    public static SitePreferencesStore SitePreferences { get; private set; } = null!;
    public static SitePermissionsStore SitePermissions { get; private set; } = null!;
    public static MemorySavedCounter MemorySaved { get; private set; } = null!;

    /// <summary>Downloads from normal windows. Each private window keeps its own list.</summary>
    public static DownloadList Downloads { get; } = new();

    /// <summary>True when the database file could not be opened and nothing will be saved this run.</summary>
    public static bool IsDatabaseTemporary { get; private set; }

    /// <summary>The engine profile shared by normal windows, known once the first web tab has started.</summary>
    public static Microsoft.Web.WebView2.Core.CoreWebView2Profile? NormalProfile { get; set; }

    public static void Initialize()
    {
        try
        {
            Database = BrowserDatabase.Open(AppPaths.Database);
        }
        catch (SqliteException)
        {
            // Full recovery (backup and start fresh) arrives in Milestone 11.
            Database = BrowserDatabase.OpenInMemory();
            IsDatabaseTemporary = true;
        }

        History = new HistoryStore(Database);
        Bookmarks = new BookmarkStore(Database);
        var settingsStore = new SettingsStore(Database);
        Settings = new BrowserSettings(settingsStore);
        MemorySaved = new MemorySavedCounter(settingsStore);
        Sessions = new SessionStore(Database);
        SitePreferences = new SitePreferencesStore(Database);
        SitePermissions = new SitePermissionsStore(Database);
    }
}
