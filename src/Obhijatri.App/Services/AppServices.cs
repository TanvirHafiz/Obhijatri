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

    /// <summary>
    /// Where the damaged database file was moved to when it had to be replaced at start-up, or null. The
    /// history, bookmarks and settings in it are lost from the browser but the file is kept for the person.
    /// </summary>
    public static string? RecoveredBackupPath { get; private set; }

    /// <summary>Appends a line to the local log (never uploaded). Logging must never cause a failure.</summary>
    private static void LogLocally(string message)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.LogFolder);
            File.AppendAllText(Path.Combine(AppPaths.LogFolder, "startup.log"), $"[{DateTimeOffset.Now:O}] {message}{Environment.NewLine}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Nowhere to write it; carry on.
        }
    }

    /// <summary>The engine profile shared by normal windows, known once the first web tab has started.</summary>
    public static Microsoft.Web.WebView2.Core.CoreWebView2Profile? NormalProfile { get; set; }

    public static void Initialize()
    {
        try
        {
            // A damaged file is moved aside and a fresh one started; the window tells the person.
            Database = BrowserDatabase.OpenOrRecover(AppPaths.Database, out var backup);
            RecoveredBackupPath = backup;
            if (backup is not null)
            {
                LogLocally("The database file was damaged. It was moved to " + backup + " and a new one was started."); // not-ui
            }
        }
        catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException)
        {
            // Not even a fresh file can be used (locked, read-only, disk full): work in memory this run.
            Database = BrowserDatabase.OpenInMemory();
            IsDatabaseTemporary = true;
            LogLocally("The database could not be opened: " + ex.Message); // not-ui
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
