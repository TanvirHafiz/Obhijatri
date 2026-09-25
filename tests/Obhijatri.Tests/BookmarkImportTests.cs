using Obhijatri.Core.Bookmarks;
using Obhijatri.Core.Storage;

namespace Obhijatri.Tests;

public sealed class BookmarkImportTests
{
    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    [Fact]
    public void Chrome_Export_KeepsStructure_AndSkipsUnsafeLinks()
    {
        var result = BookmarkHtmlImporter.Parse(Fixture("bookmarks_chrome.html"));

        // Bookmarks bar items are lifted to the top level; "Other bookmarks" stays a folder.
        Assert.Equal(["প্রথম আলো", "Tom & Jerry", "News", "https://no-title.example.org/", "Other bookmarks"],
            result.Items.Select(i => i.Title));
        Assert.Equal("https://example.com/?a=1&b=2", result.Items[1].Url);

        var news = result.Items[2];
        Assert.True(news.IsFolder);
        Assert.Equal(["The Daily Star", "Local"], news.Children.Select(c => c.Title));
        Assert.Equal("https://bdnews24.com/", Assert.Single(news.Children[1].Children).Url);

        Assert.Equal("http://old.example.net/page", Assert.Single(result.Items[4].Children).Url);

        Assert.Equal(6, result.BookmarkCount);
        Assert.Equal(2, result.SkippedCount); // javascript: and file:
    }

    [Fact]
    public void Edge_FavoritesBar_IsLifted()
    {
        var result = BookmarkHtmlImporter.Parse(Fixture("bookmarks_edge.html"));
        Assert.Equal("https://www.bing.com/", Assert.Single(result.Items).Url);
    }

    [Fact]
    public void Import_IntoStore_CreatesFolderWithEverything()
    {
        using var db = BrowserDatabase.OpenInMemory();
        var store = new BookmarkStore(db);
        var parsed = BookmarkHtmlImporter.Parse(Fixture("bookmarks_chrome.html"));

        var added = store.Import(parsed.Items, "আমদানি করা");

        Assert.Equal(6, added);
        var folder = Assert.Single(store.GetChildren(null));
        Assert.True(folder.IsFolder);
        Assert.Equal("আমদানি করা", folder.Title);
        Assert.Equal(5, store.GetChildren(folder.Id).Count);
        Assert.True(store.IsBookmarked("https://bdnews24.com/"));
        Assert.False(store.IsBookmarked("javascript:alert(document.cookie)"));
    }

    [Fact]
    public void Garbage_And_DeepNesting_DoNotThrow()
    {
        Assert.Empty(BookmarkHtmlImporter.Parse("<html><body>not bookmarks</body></html>").Items);

        var deep = string.Concat(Enumerable.Repeat("<DT><H3>f</H3><DL><p>", 500))
            + "<DT><A HREF=\"https://deep.example.com/\">deep</A>"
            + string.Concat(Enumerable.Repeat("</DL><p>", 500));
        Assert.Equal(1, BookmarkHtmlImporter.Parse(deep).BookmarkCount);
    }
}
