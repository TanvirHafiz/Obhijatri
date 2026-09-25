namespace Obhijatri.Core;

/// <summary>Defaults used until the settings store arrives in Milestone 2.</summary>
public static class BrowserDefaults
{
    public const string HomeUrl = "https://www.google.com/";

    /// <summary>
    /// Language passed to the WebView2 engine for its own menus and dialogs. The runtime ships
    /// only a "bn-IN" Bangla locale pack; plain "bn" does not resolve to it and falls back to English.
    /// </summary>
    public const string EngineLanguage = "bn-IN";

    /// <summary>UI language used for resource lookups. The toggle arrives in Milestone 3.</summary>
    public const string UiLanguage = "bn-BD";
}
