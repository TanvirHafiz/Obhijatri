using System.IO.Compression;
using System.Net;
using System.Text;
using Obhijatri.Safety.Filtering;

namespace Obhijatri.Tests;

public sealed class FilterListStoreTests : IDisposable
{
    private static readonly FilterListSource Source = new("testlist", "https://lists.example/test.txt", "Test List");

    private readonly string _root = Path.Combine(Path.GetTempPath(), "obhijatri-filters-" + Guid.NewGuid().ToString("N"));
    private readonly string _downloads;
    private readonly string _bundled;
    private readonly ManualTimeProvider _time = new(new DateTimeOffset(2026, 9, 26, 0, 0, 0, TimeSpan.Zero));

    public FilterListStoreTests()
    {
        _downloads = Path.Combine(_root, "downloads");
        _bundled = Path.Combine(_root, "bundled");
        Directory.CreateDirectory(_bundled);
        WriteGzip(Path.Combine(_bundled, "testlist.txt.gz"), List("||bundled.example^", 1200));
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private FilterListStore Store() => new(_downloads, _bundled, [Source], _time);

    private static string List(string rule, int extraLines, string title = "Test List") =>
        $"[Adblock Plus 2.0]\n! Version: 202609260555\n! Title: {title}\n{rule}\n" + string.Concat(Enumerable.Repeat("! filler\n", extraLines));

    private static void WriteGzip(string path, string text)
    {
        using var file = File.Create(path);
        using var gzip = new GZipStream(file, CompressionLevel.Optimal);
        gzip.Write(Encoding.UTF8.GetBytes(text));
    }

    private static HttpClient Http(HttpStatusCode status, string body) => new(new FakeHandler(status, body));

    [Fact]
    public void WithoutDownloads_UsesBundledCopy()
    {
        var store = Store();
        Assert.Contains("||bundled.example^", store.ReadAllLines());
        Assert.False(store.Status()[0].FromDownload);
        Assert.True(store.IsUpdateDue());
    }

    [Fact]
    public async Task GoodDownload_IsUsed_AndRecorded()
    {
        var store = Store();
        Assert.Equal(1, await store.UpdateAsync(Http(HttpStatusCode.OK, List("||fresh.example^", 1300)), CancellationToken.None));

        Assert.Contains("||fresh.example^", store.ReadAllLines());
        Assert.DoesNotContain("||bundled.example^", store.ReadAllLines());
        var status = store.Status()[0];
        Assert.True(status.FromDownload);
        Assert.Equal("202609260555", status.Version);
        Assert.False(store.IsUpdateDue());

        _time.Advance(TimeSpan.FromDays(8));
        Assert.True(store.IsUpdateDue());
    }

    [Fact]
    public async Task EditedFile_FailsTheHashCheck_AndFallsBack()
    {
        var store = Store();
        await store.UpdateAsync(Http(HttpStatusCode.OK, List("||fresh.example^", 1300)), CancellationToken.None);
        File.AppendAllText(Path.Combine(_downloads, "testlist.txt"), "@@||*^\n");

        Assert.Contains("||bundled.example^", store.ReadAllLines());
        Assert.False(store.Status()[0].FromDownload);
    }

    [Theory]
    [InlineData("<html><body>Service unavailable</body></html>")]
    [InlineData("[Adblock Plus 2.0]\n! Title: Other List\n||x.example^\n")]
    public async Task WrongContent_IsRejected(string body)
    {
        var store = Store();
        Assert.Equal(0, await store.UpdateAsync(Http(HttpStatusCode.OK, body), CancellationToken.None));
        Assert.Contains("||bundled.example^", store.ReadAllLines());
    }

    [Fact]
    public async Task TruncatedList_IsRejected()
    {
        var store = Store();
        // A valid header but far fewer lines than the list in use.
        Assert.Equal(0, await store.UpdateAsync(Http(HttpStatusCode.OK, List("||short.example^", 100)), CancellationToken.None));
        Assert.Contains("||bundled.example^", store.ReadAllLines());
    }

    [Fact]
    public async Task ServerError_KeepsWhatWeHave()
    {
        var store = Store();
        Assert.Equal(0, await store.UpdateAsync(Http(HttpStatusCode.InternalServerError, "oops"), CancellationToken.None));
        Assert.Contains("||bundled.example^", store.ReadAllLines());
    }

    private sealed class FakeHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "text/plain") });
    }
}
