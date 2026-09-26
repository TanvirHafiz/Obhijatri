using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Obhijatri.Safety.ScamShield.SafeBrowsing;

namespace Obhijatri.Tests;

public sealed class SafeBrowsingClientTests : IDisposable
{
    private readonly string _prefixFile = Path.Combine(Path.GetTempPath(), "obhijatri-sb-" + Guid.NewGuid().ToString("N") + ".txt");

    public void Dispose()
    {
        if (File.Exists(_prefixFile))
        {
            File.Delete(_prefixFile);
        }
    }

    [Fact]
    public async Task WithoutAnApiKey_NeverContactsTheServer()
    {
        var handler = new CountingHandler(HttpStatusCode.OK, "{}");
        var client = new SafeBrowsingClient(_prefixFile);

        var updated = await client.UpdateAsync(new HttpClient(handler), "", CancellationToken.None);

        Assert.False(updated);
        Assert.Equal(0, handler.RequestCount);
        Assert.Equal(0, client.PrefixCount);
    }

    [Fact]
    public void EmptyCache_NeverFlagsAnything()
    {
        var client = new SafeBrowsingClient(_prefixFile);
        Assert.False(client.IsFlagged(new Uri("https://example.com/anything")));
    }

    [Fact]
    public async Task UpdateAsync_StoresOnlyFourBytePrefixes_AndFlagsAMatchingUrl()
    {
        var targetHash = SHA256.HashData(Encoding.UTF8.GetBytes("evil.example/"));
        var rawHashes = targetHash[..4]; // the prefix Google's Update API would send
        var body = JsonSerializer.Serialize(new
        {
            listUpdateResponses = new[]
            {
                new
                {
                    threatType = "SOCIAL_ENGINEERING",
                    responseType = "FULL_UPDATE",
                    additions = new[]
                    {
                        new { rawHashes = new { prefixSize = 4, rawHashes = Convert.ToBase64String(rawHashes) } },
                    },
                },
            },
        });

        var client = new SafeBrowsingClient(_prefixFile);
        var updated = await client.UpdateAsync(new HttpClient(new CountingHandler(HttpStatusCode.OK, body)), "test-key", CancellationToken.None);

        Assert.True(updated);
        Assert.Equal(1, client.PrefixCount);
        Assert.True(client.IsFlagged(new Uri("https://evil.example/")));
        Assert.False(client.IsFlagged(new Uri("https://clean-example.test/")));

        // The cache survives a restart.
        var reloaded = new SafeBrowsingClient(_prefixFile);
        Assert.True(reloaded.IsFlagged(new Uri("https://evil.example/")));
    }

    [Fact]
    public async Task ServerError_LeavesTheCacheUnchanged()
    {
        var client = new SafeBrowsingClient(_prefixFile);
        var updated = await client.UpdateAsync(new HttpClient(new CountingHandler(HttpStatusCode.InternalServerError, "oops")), "test-key", CancellationToken.None);

        Assert.False(updated);
        Assert.Equal(0, client.PrefixCount);
    }

    [Theory]
    [InlineData("https://example.com/", new[] { "example.com/" })]
    [InlineData("https://a.b.example.com/x/y?q=1", new[] {
        "a.b.example.com/x/y?q=1", "a.b.example.com/x/", "a.b.example.com/",
        "b.example.com/x/y?q=1", "b.example.com/x/", "b.example.com/",
        "example.com/x/y?q=1", "example.com/x/", "example.com/",
    })]
    public void Expressions_CoverHostSuffixesAndPathPrefixes(string url, string[] mustContain)
    {
        var expressions = UrlCanonicalizer.Expressions(new Uri(url)).ToHashSet();
        foreach (var expected in mustContain)
        {
            Assert.Contains(expected, expressions);
        }
    }

    private sealed class CountingHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}
