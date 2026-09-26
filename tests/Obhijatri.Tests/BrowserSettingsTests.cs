using Obhijatri.Core.Settings;
using Obhijatri.Core.Storage;

namespace Obhijatri.Tests;

public sealed class BrowserSettingsTests : IDisposable
{
    private readonly BrowserDatabase _db = BrowserDatabase.OpenInMemory();
    private readonly SettingsStore _store;
    private readonly BrowserSettings _settings;

    public BrowserSettingsTests()
    {
        _store = new SettingsStore(_db);
        _settings = new BrowserSettings(_store);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public void Defaults_AreBanglaAndSafe()
    {
        Assert.Equal(BrowserSettings.Bangla, _settings.UiLanguage);
        Assert.True(_settings.IsBangla);
        Assert.Equal("bn-IN", _settings.EngineLanguage);
        Assert.Equal("google", _settings.SearchEngine.Id);
        Assert.True(_settings.RestoreTabs);
        Assert.True(_settings.SmartScreen);
        Assert.Equal(TrackingProtection.Strict, _settings.TrackingProtection);
        Assert.True(_settings.BlockAds);
        Assert.True(_settings.HttpsOnly);
        Assert.Equal(AppTheme.System, _settings.Theme);
        Assert.True(_settings.ShowBookmarkBar);
        Assert.False(_settings.VerticalTabs);
    }

    [Fact]
    public void English_ChangesEngineLanguage()
    {
        _settings.UiLanguage = BrowserSettings.English;
        Assert.False(_settings.IsBangla);
        Assert.Equal("en-US", _settings.EngineLanguage);
    }

    [Theory]
    [InlineData("fr-FR")]
    [InlineData("")]
    [InlineData("bn")]
    public void UnknownLanguage_FallsBackToBangla(string stored)
    {
        _store.SetString(BrowserSettings.Keys.UiLanguage, stored);
        Assert.Equal(BrowserSettings.Bangla, _settings.UiLanguage);
    }

    [Theory]
    [InlineData("https://www.prothomalo.com/", true)]
    [InlineData("http://example.com/", true)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("file:///C:/Windows/win.ini", false)]
    [InlineData("prothomalo.com", false)]
    [InlineData("", false)]
    public void HomePage_OnlyAcceptsWebAddresses(string value, bool valid)
    {
        Assert.Equal(valid, BrowserSettings.IsValidHomePage(value));

        _store.SetString(BrowserSettings.Keys.HomePage, value);
        Assert.Equal(valid ? value : Obhijatri.Core.BrowserDefaults.HomeUrl, _settings.HomePage);
    }

    [Fact]
    public void SearchEngine_RoundTrips_AndUnknownFallsBack()
    {
        _settings.SearchEngine = BrowserSettings.SearchEngines[2];
        Assert.Equal("duckduckgo", _settings.SearchEngine.Id);
        Assert.StartsWith("https://duckduckgo.com/", _settings.SearchEngine.Template);

        _store.SetString(BrowserSettings.Keys.SearchEngine, "evil");
        Assert.Equal("google", _settings.SearchEngine.Id);
    }

    [Fact]
    public void Enums_RejectGarbage()
    {
        _store.SetString(BrowserSettings.Keys.Theme, "Neon");
        _store.SetString(BrowserSettings.Keys.TrackingProtection, "99");
        Assert.Equal(AppTheme.System, _settings.Theme);
        Assert.Equal(TrackingProtection.Strict, _settings.TrackingProtection);

        _settings.Theme = AppTheme.Dark;
        _settings.TrackingProtection = TrackingProtection.Basic;
        Assert.Equal(AppTheme.Dark, _settings.Theme);
        Assert.Equal(TrackingProtection.Basic, _settings.TrackingProtection);
    }

    [Fact]
    public void Changed_ReportsTheKey()
    {
        var keys = new List<string>();
        _settings.Changed += (_, key) => keys.Add(key);

        _settings.SmartScreen = false;
        _settings.Theme = AppTheme.Light;

        Assert.Equal([BrowserSettings.Keys.SmartScreen, BrowserSettings.Keys.Theme], keys);
    }
}
