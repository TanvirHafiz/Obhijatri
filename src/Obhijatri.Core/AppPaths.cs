namespace Obhijatri.Core;

public static class AppPaths
{
    /// <summary>%LOCALAPPDATA%\Obhijatri</summary>
    public static string DataRoot { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Obhijatri");

    /// <summary>WebView2 user data (cookies, cache). Kept out of the install folder.</summary>
    public static string WebViewData { get; } = Path.Combine(DataRoot, "WebView2");

    /// <summary>History, bookmarks, settings and sessions.</summary>
    public static string Database { get; } = Path.Combine(DataRoot, "obhijatri.db");

    /// <summary>Downloaded ad and tracker filter lists.</summary>
    public static string FilterFolder { get; } = Path.Combine(DataRoot, "filters");

    /// <summary>Local diagnostic logs. Never uploaded.</summary>
    public static string LogFolder { get; } = Path.Combine(DataRoot, "logs");
}
