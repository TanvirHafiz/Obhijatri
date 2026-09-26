using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Obhijatri.Safety.ScamShield;

namespace Obhijatri.Tests;

public sealed class ScamListStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "obhijatri-scamlist-" + Guid.NewGuid().ToString("N"));
    private readonly string _downloadedPath;
    private readonly string _bundledPath;
    private readonly ECDsa _signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly string _publicKeyPem;

    public ScamListStoreTests()
    {
        Directory.CreateDirectory(_root);
        _downloadedPath = Path.Combine(_root, "downloaded.json");
        _bundledPath = Path.Combine(_root, "bundled.json");
        _publicKeyPem = _signingKey.ExportSubjectPublicKeyInfoPem();
        File.WriteAllText(_bundledPath, Sign("v1", ["bundled-scam.example"]));
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Sign(string version, string[] domains)
    {
        var signature = _signingKey.SignData(ScamListStore.SignedBytes(version, domains), HashAlgorithmName.SHA256);
        return JsonSerializer.Serialize(new
        {
            version,
            domains,
            signatureBase64 = Convert.ToBase64String(signature),
        });
    }

    private ScamListStore Store() => new(_downloadedPath, _bundledPath, _publicKeyPem);

    private static HttpClient Http(HttpStatusCode status, string body) => new(new FakeHandler(status, body));

    [Fact]
    public void WithoutDownload_UsesBundledCopy()
    {
        var store = Store();
        Assert.True(store.IsListed("bundled-scam.example"));
        Assert.Equal("v1", store.Version);
        Assert.True(store.IsUpdateDue());
    }

    [Fact]
    public async Task GoodSignedDownload_Replaces_BundledList()
    {
        var store = Store();
        var ok = await store.UpdateAsync(Http(HttpStatusCode.OK, Sign("v2", ["fresh-scam.example"])), "https://list.example/scam.json", CancellationToken.None);

        Assert.True(ok);
        Assert.True(store.IsListed("fresh-scam.example"));
        Assert.False(store.IsListed("bundled-scam.example"));
        Assert.Equal("v2", store.Version);
        Assert.False(store.IsUpdateDue());
    }

    [Fact]
    public async Task TamperedList_FailsVerification_AndKeepsBundled()
    {
        var store = Store();
        var body = Sign("v2", ["fresh-scam.example"]).Replace("fresh-scam.example", "attacker.example", StringComparison.Ordinal);
        var ok = await store.UpdateAsync(Http(HttpStatusCode.OK, body), "https://list.example/scam.json", CancellationToken.None);

        Assert.False(ok);
        Assert.True(store.IsListed("bundled-scam.example"));
        Assert.False(store.IsListed("attacker.example"));
    }

    [Fact]
    public async Task WrongSigningKey_IsRejected()
    {
        var otherKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var signature = otherKey.SignData(ScamListStore.SignedBytes("v2", ["fresh-scam.example"]), HashAlgorithmName.SHA256);
        var body = JsonSerializer.Serialize(new { version = "v2", domains = new[] { "fresh-scam.example" }, signatureBase64 = Convert.ToBase64String(signature) });

        var store = Store();
        var ok = await store.UpdateAsync(Http(HttpStatusCode.OK, body), "https://list.example/scam.json", CancellationToken.None);

        Assert.False(ok);
        Assert.True(store.IsListed("bundled-scam.example"));
    }

    [Fact]
    public async Task BlankUrl_NeverFetches()
    {
        var store = Store();
        var ok = await store.UpdateAsync(new HttpClient(new FakeHandler(HttpStatusCode.OK, Sign("v2", ["fresh-scam.example"]))), "", CancellationToken.None);
        Assert.False(ok);
        Assert.True(store.IsListed("bundled-scam.example"));
    }

    [Fact]
    public void IsListed_MatchesTheSite_NotJustTheExactHost()
    {
        File.WriteAllText(_bundledPath, Sign("v1", ["scam.example"]));
        var store = Store();
        Assert.True(store.IsListed("www.scam.example"));
        Assert.False(store.IsListed("scam.example.evil.test"));
    }

    private sealed class FakeHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }
}
