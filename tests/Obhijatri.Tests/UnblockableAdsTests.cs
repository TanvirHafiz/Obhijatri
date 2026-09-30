using Obhijatri.Safety.Filtering;
using Xunit;

namespace Obhijatri.Tests;

public class UnblockableAdsTests
{
    [Theory]
    [InlineData("youtube.com", "YouTube")]
    [InlineData("www.youtube.com", "YouTube")]
    [InlineData("m.youtube.com", "YouTube")]
    [InlineData("YOUTU.BE", "YouTube")]
    [InlineData("web.facebook.com", "Facebook")]
    [InlineData("www.instagram.com", "Instagram")]
    public void KnownSitesAreReported(string host, string name) => Assert.Equal(name, UnblockableAds.NameFor(host));

    [Theory]
    [InlineData("prothomalo.com")]
    [InlineData("notyoutube.com")]
    [InlineData("youtube.com.evil.example")]
    [InlineData("")]
    [InlineData(null)]
    public void OtherSitesAreNot(string? host) => Assert.Null(UnblockableAds.NameFor(host));
}
