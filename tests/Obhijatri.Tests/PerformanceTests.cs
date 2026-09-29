using Obhijatri.Core.Performance;
using Obhijatri.Core.Settings;
using Obhijatri.Core.Storage;
using Obhijatri.Safety.LowData;

namespace Obhijatri.Tests;

public sealed class PerformanceTests : IDisposable
{
    private readonly BrowserDatabase _db = BrowserDatabase.OpenInMemory();

    public void Dispose() => _db.Dispose();

    // ---- Tab sleep policy ----

    private static bool Sleeps(
        int minutes = 10, int idleMinutes = 11, bool active = false, bool pinned = false,
        bool audio = false, bool loading = false, bool sleeping = false) =>
        TabSleepPolicy.ShouldSleep(minutes, TimeSpan.FromMinutes(idleMinutes), active, pinned, audio, loading, sleeping);

    [Fact]
    public void Sleep_IdleBackgroundTab_Sleeps() => Assert.True(Sleeps());

    [Fact]
    public void Sleep_ExactlyAtThreshold_Sleeps() => Assert.True(Sleeps(idleMinutes: 10));

    [Fact]
    public void Sleep_NotIdleLongEnough_Stays() => Assert.False(Sleeps(idleMinutes: 9));

    [Fact]
    public void Sleep_SettingOff_Never() => Assert.False(Sleeps(minutes: 0, idleMinutes: 10_000));

    [Theory]
    [InlineData(true, false, false, false, false)]
    [InlineData(false, true, false, false, false)]
    [InlineData(false, false, true, false, false)]
    [InlineData(false, false, false, true, false)]
    [InlineData(false, false, false, false, true)]
    public void Sleep_ActivePinnedAudioLoadingOrAlreadyAsleep_Skipped(bool active, bool pinned, bool audio, bool loading, bool sleeping) =>
        Assert.False(Sleeps(active: active, pinned: pinned, audio: audio, loading: loading, sleeping: sleeping));

    // ---- Settings ----

    [Fact]
    public void Settings_PerformanceDefaults()
    {
        var settings = new BrowserSettings(new SettingsStore(_db));
        Assert.Equal(10, settings.TabSleepMinutes);
        Assert.True(settings.ShowMemoryMeter);
        Assert.False(settings.LowDataMode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(60)]
    public void Settings_TabSleepMinutes_AllowedValuesRoundTrip(int minutes)
    {
        var settings = new BrowserSettings(new SettingsStore(_db)) { TabSleepMinutes = minutes };
        Assert.Equal(minutes, settings.TabSleepMinutes);
    }

    [Theory]
    [InlineData("7")]
    [InlineData("-5")]
    [InlineData("abc")]
    [InlineData("")]
    public void Settings_TabSleepMinutes_DamagedValueFallsBackToDefault(string stored)
    {
        var store = new SettingsStore(_db);
        store.SetString(BrowserSettings.Keys.TabSleepMinutes, stored);
        Assert.Equal(TabSleepPolicy.DefaultMinutes, new BrowserSettings(store).TabSleepMinutes);
    }

    [Fact]
    public void Settings_TabSleepMinutes_InvalidAssignmentStoresDefault()
    {
        var settings = new BrowserSettings(new SettingsStore(_db)) { TabSleepMinutes = 7 };
        Assert.Equal(TabSleepPolicy.DefaultMinutes, settings.TabSleepMinutes);
    }

    // ---- Memory saved today ----

    [Fact]
    public void MemorySaved_AddsUpWithinADay()
    {
        var counter = new MemorySavedCounter(new SettingsStore(_db), new ManualTimeProvider(new DateTimeOffset(2026, 9, 29, 10, 0, 0, TimeSpan.Zero)));
        counter.Add(100);
        counter.Add(250);
        Assert.Equal(350, counter.TodayBytes);
    }

    [Fact]
    public void MemorySaved_StartsAgainOnANewDay()
    {
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 9, 29, 10, 0, 0, TimeSpan.Zero));
        var counter = new MemorySavedCounter(new SettingsStore(_db), time);
        counter.Add(500);

        time.Advance(TimeSpan.FromDays(1));
        Assert.Equal(0, counter.TodayBytes);
        counter.Add(40);
        Assert.Equal(40, counter.TodayBytes);
    }

    [Fact]
    public void MemorySaved_SurvivesARestart()
    {
        var store = new SettingsStore(_db);
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 9, 29, 10, 0, 0, TimeSpan.Zero));
        new MemorySavedCounter(store, time).Add(1234);
        Assert.Equal(1234, new MemorySavedCounter(store, time).TodayBytes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public void MemorySaved_IgnoresZeroAndNegative(long bytes)
    {
        var counter = new MemorySavedCounter(new SettingsStore(_db));
        counter.Add(bytes);
        Assert.Equal(0, counter.TodayBytes);
    }

    [Fact]
    public void MemorySaved_DamagedStoredValueReadsAsZero()
    {
        var store = new SettingsStore(_db);
        var counter = new MemorySavedCounter(store);
        counter.Add(10);
        store.SetString(MemorySavedCounter.BytesKey, "not a number");
        Assert.Equal(0, counter.TodayBytes);
    }

    // ---- Pinned tabs in the session ----

    [Fact]
    public void Session_PinnedFlagRoundTrips()
    {
        var sessions = new SessionStore(_db);
        sessions.Save(
        [
            new SessionTab("https://a.example/", "a", false, IsPinned: true),
            new SessionTab("https://b.example/", "b", true),
        ]);

        var loaded = sessions.Load();
        Assert.True(loaded[0].IsPinned);
        Assert.False(loaded[1].IsPinned);
    }

    // ---- Heavy embeds ----

    [Theory]
    [InlineData("www.youtube.com", "/embed/abc123", true)]
    [InlineData("youtube.com", "/embed/abc123", true)]
    [InlineData("www.youtube-nocookie.com", "/embed/abc123", true)]
    [InlineData("player.vimeo.com", "/video/1", true)]
    [InlineData("www.facebook.com", "/plugins/video.php", true)]
    [InlineData("www.facebook.com", "/v18.0/plugins/page.php", true)]
    [InlineData("platform.twitter.com", "/embed/Tweet.html", true)]
    [InlineData("www.instagram.com", "/p/xyz/embed/", true)]
    [InlineData("open.spotify.com", "/embed/track/1", true)]
    public void HeavyEmbeds_KnownPlayers_Match(string host, string path, bool expected) =>
        Assert.Equal(expected, HeavyEmbeds.IsHeavyEmbed(host, path));

    [Theory]
    [InlineData("www.youtube.com", "/watch")]
    [InlineData("www.facebook.com", "/login.php")]
    [InlineData("www.facebook.com", "/")]
    [InlineData("www.prothomalo.com", "/embed/1")]
    [InlineData("notyoutube.com", "/embed/1")]
    [InlineData("youtube.com.evil.example", "/embed/1")]
    [InlineData("example.com", "/plugins/x")]
    public void HeavyEmbeds_OrdinaryPagesAndLookalikes_DoNotMatch(string host, string path) =>
        Assert.False(HeavyEmbeds.IsHeavyEmbed(host, path));
}
