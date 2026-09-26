using Obhijatri.Core.Storage;

namespace Obhijatri.Core.Settings;

public enum AppTheme
{
    System,
    Light,
    Dark,
}

public enum TrackingProtection
{
    Basic,
    Balanced,
    Strict,
}

public sealed record SearchEngine(string Id, string NameKey, string Template);

/// <summary>
/// Typed access to user settings with defaults. Values read from the database are validated,
/// so a damaged or edited value falls back to the safe default instead of being trusted.
/// </summary>
public sealed class BrowserSettings
{
    public const string Bangla = "bn-BD";
    public const string English = "en-US";

    public static readonly IReadOnlyList<string> UiLanguages = [Bangla, English];

    public static readonly IReadOnlyList<SearchEngine> SearchEngines =
    [
        new("google", "SearchEngineGoogle", AddressResolver.GoogleSearchTemplate),
        new("bing", "SearchEngineBing", "https://www.bing.com/search?q={0}"),
        new("duckduckgo", "SearchEngineDuckDuckGo", "https://duckduckgo.com/?q={0}"),
    ];

    private readonly SettingsStore _store;

    public BrowserSettings(SettingsStore store)
    {
        _store = store;
    }

    /// <summary>Raised after a setting is changed through this class.</summary>
    public event EventHandler<string>? Changed;

    // ---- General ----

    /// <summary>UI language. Takes effect after a restart.</summary>
    public string UiLanguage
    {
        get => _store.GetString(Keys.UiLanguage) is { } value && UiLanguages.Contains(value) ? value : Bangla;
        set => SetString(Keys.UiLanguage, UiLanguages.Contains(value) ? value : Bangla);
    }

    public bool IsBangla => UiLanguage == Bangla;

    /// <summary>
    /// Language for the web engine's own menus and for the language websites are asked for.
    /// The WebView2 runtime ships only a "bn-IN" Bangla pack; plain "bn" falls back to English.
    /// </summary>
    public string EngineLanguage => IsBangla ? "bn-IN" : "en-US";

    public string HomePage
    {
        get => _store.GetString(Keys.HomePage) is { } value && IsValidHomePage(value) ? value : BrowserDefaults.HomeUrl;
        set => SetString(Keys.HomePage, IsValidHomePage(value) ? value : BrowserDefaults.HomeUrl);
    }

    public SearchEngine SearchEngine
    {
        get => SearchEngines.FirstOrDefault(e => e.Id == _store.GetString(Keys.SearchEngine)) ?? SearchEngines[0];
        set => SetString(Keys.SearchEngine, SearchEngines.Any(e => e.Id == value.Id) ? value.Id : SearchEngines[0].Id);
    }

    public bool RestoreTabs
    {
        get => _store.GetBool(Keys.RestoreTabs, true);
        set => SetBool(Keys.RestoreTabs, value);
    }

    /// <summary>Bangla phonetic typing in the address bar (web pages remember it per site instead).</summary>
    public bool AddressBarPhonetic
    {
        get => _store.GetBool(Keys.AddressBarPhonetic, false);
        set => SetBool(Keys.AddressBarPhonetic, value);
    }

    // ---- Security and privacy ----

    /// <summary>Microsoft Defender SmartScreen checks for pages and downloads.</summary>
    public bool SmartScreen
    {
        get => _store.GetBool(Keys.SmartScreen, true);
        set => SetBool(Keys.SmartScreen, value);
    }

    public TrackingProtection TrackingProtection
    {
        get => Enum.TryParse<TrackingProtection>(_store.GetString(Keys.TrackingProtection), out var value) && Enum.IsDefined(value)
            ? value
            : TrackingProtection.Balanced;
        set => SetString(Keys.TrackingProtection, value.ToString());
    }

    // ---- Appearance ----

    public AppTheme Theme
    {
        get => Enum.TryParse<AppTheme>(_store.GetString(Keys.Theme), out var value) && Enum.IsDefined(value) ? value : AppTheme.System;
        set => SetString(Keys.Theme, value.ToString());
    }

    public bool ShowBookmarkBar
    {
        get => _store.GetBool(Keys.ShowBookmarkBar, true);
        set => SetBool(Keys.ShowBookmarkBar, value);
    }

    public bool VerticalTabs
    {
        get => _store.GetBool(Keys.VerticalTabs, false);
        set => SetBool(Keys.VerticalTabs, value);
    }

    /// <summary>A home page must be a plain http or https address.</summary>
    public static bool IsValidHomePage(string? value) =>
        value is { Length: > 0 and <= HistoryStore.MaxUrlLength }
        && Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
        && !string.IsNullOrEmpty(uri.Host);

    private void SetString(string key, string value)
    {
        _store.SetString(key, value);
        Changed?.Invoke(this, key);
    }

    private void SetBool(string key, bool value)
    {
        _store.SetBool(key, value);
        Changed?.Invoke(this, key);
    }

    public static class Keys
    {
        public const string UiLanguage = "ui.language";
        public const string HomePage = "general.homePage";
        public const string SearchEngine = "general.searchEngine";
        public const string RestoreTabs = "general.restoreTabs";
        public const string AddressBarPhonetic = "typing.addressBarPhonetic";
        public const string SmartScreen = "security.smartScreen";
        public const string TrackingProtection = "privacy.trackingProtection";
        public const string Theme = "appearance.theme";
        public const string ShowBookmarkBar = SettingKeys.ShowBookmarkBar;
        public const string VerticalTabs = SettingKeys.VerticalTabs;
    }
}
