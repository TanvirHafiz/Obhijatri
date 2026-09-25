using Obhijatri.Core.Storage;

namespace Obhijatri.Tests;

public sealed class BookmarkStoreTests : IDisposable
{
    private readonly BrowserDatabase _db = BrowserDatabase.OpenInMemory();
    private readonly BookmarkStore _bookmarks;

    public BookmarkStoreTests()
    {
        _bookmarks = new BookmarkStore(_db);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public void AddBookmark_AppearsOnBarInOrder()
    {
        _bookmarks.AddBookmark(null, "প্রথম আলো", "https://www.prothomalo.com/");
        _bookmarks.AddBookmark(null, "Daily Star", "https://www.thedailystar.net/");

        var bar = _bookmarks.GetChildren(null);

        Assert.Equal(["প্রথম আলো", "Daily Star"], bar.Select(b => b.Title));
        Assert.Equal([0, 1], bar.Select(b => b.Position));
        Assert.All(bar, b => Assert.False(b.IsFolder));
    }

    [Fact]
    public void Folders_HoldChildren_AndDeleteCascades()
    {
        var news = _bookmarks.AddFolder(null, "News");
        var local = _bookmarks.AddFolder(news, "Local");
        _bookmarks.AddBookmark(news, "Star", "https://www.thedailystar.net/");
        _bookmarks.AddBookmark(local, "bdnews24", "https://bdnews24.com/");

        Assert.Equal(2, _bookmarks.GetChildren(news).Count);
        Assert.Single(_bookmarks.GetChildren(local));

        _bookmarks.Delete(news);

        Assert.Empty(_bookmarks.GetChildren(null));
        Assert.Null(_bookmarks.Get(local));
        Assert.False(_bookmarks.IsBookmarked("https://bdnews24.com/"));
    }

    [Fact]
    public void IsBookmarked_And_RemoveByUrl()
    {
        _bookmarks.AddBookmark(null, "a", "https://example.com/");
        var folder = _bookmarks.AddFolder(null, "f");
        _bookmarks.AddBookmark(folder, "a again", "https://example.com/");

        Assert.True(_bookmarks.IsBookmarked("https://example.com/"));
        Assert.Equal(2, _bookmarks.RemoveByUrl("https://example.com/"));
        Assert.False(_bookmarks.IsBookmarked("https://example.com/"));
        Assert.NotNull(_bookmarks.Get(folder));
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///C:/Windows/win.ini")]
    [InlineData("data:text/html,x")]
    public void AddBookmark_RejectsNonWebAddresses(string url)
    {
        Assert.Throws<ArgumentException>(() => _bookmarks.AddBookmark(null, "bad", url));
    }

    [Fact]
    public void Rename_TrimsTitle()
    {
        var id = _bookmarks.AddBookmark(null, "old", "https://example.com/");
        _bookmarks.Rename(id, "  নতুন নাম  ");
        Assert.Equal("নতুন নাম", _bookmarks.Get(id)!.Title);
    }

    [Fact]
    public void Move_ReordersWithinBar()
    {
        var a = _bookmarks.AddBookmark(null, "a", "https://a.example.com/");
        var b = _bookmarks.AddBookmark(null, "b", "https://b.example.com/");
        var c = _bookmarks.AddBookmark(null, "c", "https://c.example.com/");

        _bookmarks.Move(c, null, 0);

        Assert.Equal([c, a, b], _bookmarks.GetChildren(null).Select(x => x.Id));
    }

    [Fact]
    public void Move_IntoFolder_AndRefusesCycles()
    {
        var outer = _bookmarks.AddFolder(null, "outer");
        var inner = _bookmarks.AddFolder(outer, "inner");
        var link = _bookmarks.AddBookmark(null, "link", "https://example.com/");

        _bookmarks.Move(link, inner, 0);

        Assert.Equal(inner, _bookmarks.Get(link)!.ParentId);
        Assert.Throws<InvalidOperationException>(() => _bookmarks.Move(outer, inner, 0));
        Assert.Throws<InvalidOperationException>(() => _bookmarks.Move(outer, outer, 0));
    }

    [Fact]
    public void Changed_IsRaisedOnEdits()
    {
        var count = 0;
        _bookmarks.Changed += (_, _) => count++;

        var id = _bookmarks.AddBookmark(null, "a", "https://example.com/");
        _bookmarks.Rename(id, "b");
        _bookmarks.Delete(id);

        Assert.Equal(3, count);
    }
}
