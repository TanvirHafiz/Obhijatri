using Obhijatri.Bangla.Calendars;
using Obhijatri.Bangla.Prayer;
using Obhijatri.Core.Performance;
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
            : TrackingProtection.Strict;
        set => SetString(Keys.TrackingProtection, value.ToString());
    }

    /// <summary>Block ads and trackers with the filter lists.</summary>
    public bool BlockAds
    {
        get => _store.GetBool(Keys.BlockAds, true);
        set => SetBool(Keys.BlockAds, value);
    }

    /// <summary>Open http:// addresses as https://, with a warning page if a site has no HTTPS.</summary>
    public bool HttpsOnly
    {
        get => _store.GetBool(Keys.HttpsOnly, true);
        set => SetBool(Keys.HttpsOnly, value);
    }

    /// <summary>Lookalike domain and known-scam warnings (Milestone 6).</summary>
    public bool ScamShieldEnabled
    {
        get => _store.GetBool(Keys.ScamShieldEnabled, true);
        set => SetBool(Keys.ScamShieldEnabled, value);
    }

    /// <summary>Where the signed BD scam list is fetched from. Blank (the default) skips fetching it.</summary>
    public string ScamListUrl
    {
        get => _store.GetString(Keys.ScamListUrl) ?? string.Empty;
        set => SetString(Keys.ScamListUrl, value.Trim());
    }

    /// <summary>Checks a submitted password against Have I Been Pwned (k-anonymity, Milestone 7).</summary>
    public bool PasswordLeakCheckEnabled
    {
        get => _store.GetBool(Keys.PasswordLeakCheckEnabled, true);
        set => SetBool(Keys.PasswordLeakCheckEnabled, value);
    }

    /// <summary>Warns when a page writes to the clipboard without a recent click or keypress.</summary>
    public bool ClipboardGuardEnabled
    {
        get => _store.GetBool(Keys.ClipboardGuardEnabled, true);
        set => SetBool(Keys.ClipboardGuardEnabled, value);
    }

    /// <summary>Blocks third-party scripts and shows a "safe mode" badge on banking and payment sites.</summary>
    public bool PaymentLockEnabled
    {
        get => _store.GetBool(Keys.PaymentLockEnabled, true);
        set => SetBool(Keys.PaymentLockEnabled, value);
    }

    /// <summary>Deletes cookies for sites that are not bookmarked when the browser closes.</summary>
    public bool CookieAutoDeleteEnabled
    {
        get => _store.GetBool(Keys.CookieAutoDeleteEnabled, false);
        set => SetBool(Keys.CookieAutoDeleteEnabled, value);
    }

    /// <summary>
    /// Layer 2 of "এটা কি প্রতারণা?": send the page text to an Ollama server on this computer for a plain
    /// Bangla explanation. Off by default. The page text never goes anywhere but this computer.
    /// </summary>
    public bool OllamaEnabled
    {
        get => _store.GetBool(Keys.OllamaEnabled, false);
        set => SetBool(Keys.OllamaEnabled, value);
    }

    public const string DefaultOllamaModel = "llama3.2";

    /// <summary>The Ollama model to ask. A damaged or odd value falls back to the default.</summary>
    public string OllamaModel
    {
        get => _store.GetString(Keys.OllamaModel) is { } value && IsValidOllamaModel(value) ? value : DefaultOllamaModel;
        set => SetString(Keys.OllamaModel, IsValidOllamaModel(value.Trim()) ? value.Trim() : DefaultOllamaModel);
    }

    /// <summary>Letters, digits and . _ : / - only, at most 80 characters, starting with a letter or digit.</summary>
    public static bool IsValidOllamaModel(string? value) =>
        value is { Length: > 0 and <= 80 }
        && char.IsAsciiLetterOrDigit(value[0])
        && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or ':' or '/' or '-');

    /// <summary>
    /// The "translate with Google" route (off by default). Google is sent the page address and fetches
    /// the page itself. Never offered in private windows or on banking and payment sites.
    /// </summary>
    public bool GoogleTranslateEnabled
    {
        get => _store.GetBool(Keys.GoogleTranslateEnabled, false);
        set => SetBool(Keys.GoogleTranslateEnabled, value);
    }

    /// <summary>The person has read the first-use warning about sending the page address to Google.</summary>
    public bool GoogleTranslateWarned
    {
        get => _store.GetBool(Keys.GoogleTranslateWarned, false);
        set => SetBool(Keys.GoogleTranslateWarned, value);
    }

    // ---- New tab page (Milestone 9) ----

    /// <summary>The district whose prayer times the new tab page shows (an id from the districts list).</summary>
    public string PrayerDistrict
    {
        get => _store.GetString(Keys.PrayerDistrict) is { } value && Districts.IsValidId(value) ? value : Districts.DefaultId;
        set => SetString(Keys.PrayerDistrict, Districts.IsValidId(value) ? value : Districts.DefaultId);
    }

    /// <summary>Days added to the Saudi (Umm al-Qura) Hijri date to follow Bangladesh's moon sighting.</summary>
    public int HijriAdjustment
    {
        get => int.TryParse(_store.GetString(Keys.HijriAdjustment), System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out var value)
               && HijriCalendar.IsValidAdjustment(value)
            ? value
            : 0;
        set => SetString(Keys.HijriAdjustment,
            (HijriCalendar.IsValidAdjustment(value) ? value : 0).ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    // ---- Performance (Milestone 8) ----

    /// <summary>Minutes a background tab may sit idle before it sleeps. 0 means never.</summary>
    public int TabSleepMinutes
    {
        get => int.TryParse(_store.GetString(Keys.TabSleepMinutes), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var value)
               && TabSleepPolicy.IsValidMinutes(value)
            ? value
            : TabSleepPolicy.DefaultMinutes;
        set => SetString(Keys.TabSleepMinutes,
            (TabSleepPolicy.IsValidMinutes(value) ? value : TabSleepPolicy.DefaultMinutes).ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>Shows the total memory use in the toolbar.</summary>
    public bool ShowMemoryMeter
    {
        get => _store.GetBool(Keys.ShowMemoryMeter, true);
        set => SetBool(Keys.ShowMemoryMeter, value);
    }

    /// <summary>Blocks video autoplay, loads images only when scrolled to, and blocks heavy embeds.</summary>
    public bool LowDataMode
    {
        get => _store.GetBool(Keys.LowDataMode, false);
        set => SetBool(Keys.LowDataMode, value);
    }

    // ---- Appearance ----

    public AppTheme Theme
    {
        get => Enum.TryParse<AppTheme>(_store.GetString(Keys.Theme), out var value) && Enum.IsDefined(value) ? value : AppTheme.System;
        set => SetString(Keys.Theme, value.ToString());
    }

    /// <summary>Draws Bangla text on web pages in a good system Bangla font instead of the site's own.</summary>
    public bool FixBanglaFonts
    {
        get => _store.GetBool(Keys.FixBanglaFonts, false);
        set => SetBool(Keys.FixBanglaFonts, value);
    }

    /// <summary>Text size in reader mode, 14 to 32 (device independent pixels).</summary>
    public int ReaderFontSize
    {
        get => int.TryParse(_store.GetString(Keys.ReaderFontSize), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? Math.Clamp(value, MinReaderFontSize, MaxReaderFontSize)
            : DefaultReaderFontSize;
        set => SetString(Keys.ReaderFontSize,
            Math.Clamp(value, MinReaderFontSize, MaxReaderFontSize).ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    public const int MinReaderFontSize = 14;
    public const int MaxReaderFontSize = 32;
    public const int DefaultReaderFontSize = 20;

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
        public const string HttpsOnly = "security.httpsOnly";
        public const string ScamShieldEnabled = "security.scamShieldEnabled";
        public const string ScamListUrl = "security.scamListUrl";
        public const string PasswordLeakCheckEnabled = "security.passwordLeakCheckEnabled";
        public const string ClipboardGuardEnabled = "security.clipboardGuardEnabled";
        public const string PaymentLockEnabled = "security.paymentLockEnabled";
        public const string CookieAutoDeleteEnabled = "privacy.cookieAutoDeleteEnabled";
        public const string BlockAds = "privacy.blockAds";
        public const string TrackingProtection = "privacy.trackingProtection";
        public const string GoogleTranslateEnabled = "translate.googleEnabled";
        public const string GoogleTranslateWarned = "translate.googleWarned";
        public const string OllamaEnabled = "ai.ollamaEnabled";
        public const string OllamaModel = "ai.ollamaModel";
        public const string PrayerDistrict = "newtab.prayerDistrict";
        public const string HijriAdjustment = "newtab.hijriAdjustment";
        public const string TabSleepMinutes = "performance.tabSleepMinutes";
        public const string ShowMemoryMeter = "performance.showMemoryMeter";
        public const string LowDataMode = "performance.lowDataMode";
        public const string Theme = "appearance.theme";
        public const string FixBanglaFonts = "appearance.fixBanglaFonts";
        public const string ReaderFontSize = "appearance.readerFontSize";
        public const string ShowBookmarkBar = SettingKeys.ShowBookmarkBar;
        public const string VerticalTabs = SettingKeys.VerticalTabs;
    }
}
