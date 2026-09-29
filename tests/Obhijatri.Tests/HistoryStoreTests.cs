using Obhijatri.Core.Storage;

namespace Obhijatri.Tests;

public sealed class HistoryStoreTests : IDisposable
{
    private readonly BrowserDatabase _db = BrowserDatabase.OpenInMemory();
    private readonly ManualTimeProvider _time = new(new DateTimeOffset(2026, 4, 14, 10, 0, 0, TimeSpan.Zero));
    private readonly HistoryStore _history;

    public HistoryStoreTests()
    {
        _history = new HistoryStore(_db, _time);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public void TopSites_GroupsByHost_MostVisitedFirst_FrontPageAddress()
    {
        for (var i = 0; i < 3; i++)
        {
            _history.AddVisit("https://www.prothomalo.com/bangladesh/article" + i, "a");
        }
        _history.AddVisit("https://bdnews24.com/", "b");
        _history.AddVisit("https://bdnews24.com/", "b");
        _history.AddVisit("https://example.org/page", "c");
        _history.AddVisit("obhijatri://history", "d");

        var top = _history.TopSites(10);

        Assert.Equal(["prothomalo.com", "bdnews24.com", "example.org"], top.Select(t => t.Host));
        Assert.Equal("https://www.prothomalo.com/", top[0].Url);
        Assert.Equal("https://example.org/", top[2].Url);
    }

    [Fact]
    public void TopSites_IgnoresOldVisits_AndHonoursLimit()
    {
        _history.AddVisit("https://old.example/", "old");
        _time.Advance(TimeSpan.FromDays(61));
        _history.AddVisit("https://a.example/", "a");
        _history.AddVisit("https://b.example/", "b");

        var top = _history.TopSites(1);

        Assert.Single(top);
        Assert.NotEqual("old.example", top[0].Host);
    }

    [Fact]
    public void AddVisit_StoresEntry_NewestFirst()
    {
        _history.AddVisit("https://www.prothomalo.com/", "প্রথম আলো");
        _time.Advance(TimeSpan.FromMinutes(1));
        _history.AddVisit("https://bdnews24.com/", "bdnews24");

        var all = _history.Search(null);

        Assert.Equal(2, all.Count);
        Assert.Equal("https://bdnews24.com/", all[0].Url);
        Assert.Equal("প্রথম আলো", all[1].Title);
        Assert.Equal(_time.Now.AddMinutes(-1), all[1].VisitedAt);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///C:/secret.txt")]
    [InlineData("about:blank")]
    [InlineData("data:text/html,hi")]
    [InlineData("not a url")]
    public void AddVisit_IgnoresNonWebAddresses(string url)
    {
        Assert.Null(_history.AddVisit(url, "x"));
        Assert.Equal(0, _history.Count());
    }

    [Fact]
    public void Search_MatchesTitleOrUrl()
    {
        _history.AddVisit("https://www.thedailystar.net/", "The Daily Star");
        _history.AddVisit("https://www.prothomalo.com/", "প্রথম আলো | বাংলা নিউজ পেপার");
        _history.AddVisit("https://example.com/", "Example");

        Assert.Single(_history.Search("daily"));
        Assert.Single(_history.Search("প্রথম"));
        Assert.Single(_history.Search("prothomalo"));
        Assert.Empty(_history.Search("nothing-here"));
    }

    [Fact]
    public void Search_TreatsLikeWildcardsLiterally()
    {
        _history.AddVisit("https://example.com/a", "100% real");
        _history.AddVisit("https://example.com/b", "under_score");
        _history.AddVisit("https://example.com/c", "plain");

        Assert.Single(_history.Search("%"));
        Assert.Single(_history.Search("_"));
    }

    [Fact]
    public void UpdateLatestTitle_ChangesOnlyNewestVisitOfThatUrl()
    {
        _history.AddVisit("https://example.com/", "old");
        _time.Advance(TimeSpan.FromMinutes(5));
        _history.AddVisit("https://example.com/", "");

        _history.UpdateLatestTitle("https://example.com/", "new");

        var all = _history.Search(null);
        Assert.Equal("new", all[0].Title);
        Assert.Equal("old", all[1].Title);
    }

    [Fact]
    public void ClearLastHour_KeepsOlderEntries()
    {
        _history.AddVisit("https://old.example.com/", "old");
        _time.Advance(TimeSpan.FromHours(2));
        _history.AddVisit("https://recent.example.com/", "recent");
        _time.Advance(TimeSpan.FromMinutes(30));

        var removed = _history.ClearLast(TimeSpan.FromHours(1));

        Assert.Equal(1, removed);
        Assert.Equal("https://old.example.com/", Assert.Single(_history.Search(null)).Url);
    }

    [Fact]
    public void ClearLastDay_And_ClearAll()
    {
        _history.AddVisit("https://two-days.example.com/", "a");
        _time.Advance(TimeSpan.FromDays(2));
        _history.AddVisit("https://today.example.com/", "b");

        Assert.Equal(1, _history.ClearLast(TimeSpan.FromDays(1)));
        Assert.Equal(1, _history.Count());
        Assert.Equal(1, _history.ClearAll());
        Assert.Equal(0, _history.Count());
    }

    [Fact]
    public void Delete_RemovesSingleEntry()
    {
        var keep = _history.AddVisit("https://a.example.com/", "a");
        var remove = _history.AddVisit("https://b.example.com/", "b");

        _history.Delete(remove!.Value);

        Assert.Equal(keep, Assert.Single(_history.Search(null)).Id);
    }

    [Fact]
    public void LongTitles_AreTruncated()
    {
        _history.AddVisit("https://example.com/", new string('x', 5000));
        Assert.Equal(HistoryStore.MaxTitleLength, _history.Search(null)[0].Title.Length);
    }

    [Fact]
    public void History_PersistsAcrossReopen()
    {
        var path = Path.Combine(Path.GetTempPath(), $"obhijatri-test-{Guid.NewGuid():N}.db");
        try
        {
            using (var db = BrowserDatabase.Open(path))
            {
                new HistoryStore(db).AddVisit("https://persist.example.com/", "persist");
            }

            using (var db = BrowserDatabase.Open(path))
            {
                Assert.Equal("persist", Assert.Single(new HistoryStore(db).Search(null)).Title);
            }
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
