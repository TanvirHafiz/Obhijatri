using System.Net;
using System.Text;
using Obhijatri.Safety.Privacy;

namespace Obhijatri.Tests;

public sealed class HibpClientTests
{
    private static readonly HibpClient Client = new();

    [Fact]
    public void Sha1Hex_KnownPassword_MatchesTheWellKnownDigest()
    {
        // "password" -> 5BAA61E4C9B93F3F0682250B6CF8331B7EE68FD, the example HIBP's own docs use.
        Assert.Equal("5BAA61E4C9B93F3F0682250B6CF8331B7EE68FD8", HibpClient.Sha1Hex("password"));
    }

    [Fact]
    public async Task CheckHashAsync_OnlySendsTheFirstFiveCharacters()
    {
        var hash = HibpClient.Sha1Hex("password123");
        var handler = new RecordingHandler(HttpStatusCode.OK, hash[5..] + ":42\r\nAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA:1\r\n");

        var count = await Client.CheckHashAsync(new HttpClient(handler), hash, CancellationToken.None);

        Assert.NotNull(handler.RequestedUri);
        var requestedPrefix = handler.RequestedUri!.Segments[^1];
        Assert.Equal(5, requestedPrefix.Length);
        Assert.Equal(hash[..5], requestedPrefix, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain(hash[5..], handler.RequestedUri.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(42, count);
    }

    [Fact]
    public async Task CheckHashAsync_SuffixNotInResponse_ReturnsZero()
    {
        var hash = HibpClient.Sha1Hex("a-clean-password-nobody-uses");
        var handler = new RecordingHandler(HttpStatusCode.OK, "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA:5\r\n");

        Assert.Equal(0, await Client.CheckHashAsync(new HttpClient(handler), hash, CancellationToken.None));
    }

    [Fact]
    public async Task CheckHashAsync_ServerError_ReturnsNull()
    {
        var hash = HibpClient.Sha1Hex("whatever");
        var handler = new RecordingHandler(HttpStatusCode.InternalServerError, "oops");

        Assert.Null(await Client.CheckHashAsync(new HttpClient(handler), hash, CancellationToken.None));
    }

    [Theory]
    [InlineData("too-short")]
    [InlineData("")]
    public async Task CheckHashAsync_NotAValidHash_NeverSendsARequest(string bad)
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, "");
        Assert.Null(await Client.CheckHashAsync(new HttpClient(handler), bad, CancellationToken.None));
        Assert.Null(handler.RequestedUri);
    }

    private sealed class RecordingHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public Uri? RequestedUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestedUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "text/plain") });
        }
    }
}
