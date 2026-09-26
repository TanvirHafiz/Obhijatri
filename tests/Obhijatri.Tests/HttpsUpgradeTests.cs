using Obhijatri.Safety;

namespace Obhijatri.Tests;

public sealed class HttpsUpgradeTests
{
    [Theory]
    [InlineData("http://www.prothomalo.com/", true)]
    [InlineData("http://bangladesh.gov.bd/site/page", true)]
    [InlineData("http://8.8.8.8/", true)]
    [InlineData("https://www.prothomalo.com/", false)]
    [InlineData("http://localhost:3000/", false)]
    [InlineData("http://127.0.0.1/", false)]
    [InlineData("http://192.168.0.1/", false)]
    [InlineData("http://10.0.0.5/", false)]
    [InlineData("http://172.20.1.1/", false)]
    [InlineData("http://172.32.1.1/", true)]
    [InlineData("http://router/", false)]
    [InlineData("http://printer.local/", false)]
    [InlineData("http://app.test/", false)]
    [InlineData("http://[::1]/", false)]
    public void Upgrade_OnlyPublicHttpAddresses(string url, bool expected)
    {
        Assert.Equal(expected, HttpsUpgrade.ShouldUpgrade(new Uri(url)));
    }

    [Theory]
    [InlineData("http://example.com/a?b=1#c", "https://example.com/a?b=1#c")]
    [InlineData("http://example.com:8080/x", "https://example.com:8080/x")]
    public void ToHttps_KeepsPathQueryAndCustomPort(string input, string expected)
    {
        Assert.Equal(expected, HttpsUpgrade.ToHttps(new Uri(input)).AbsoluteUri);
    }
}
