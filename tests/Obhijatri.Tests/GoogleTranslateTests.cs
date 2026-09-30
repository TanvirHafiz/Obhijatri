using Obhijatri.Safety.Translate;

namespace Obhijatri.Tests;

public sealed class GoogleTranslateTests
{
    private static string? Build(string url, bool isPrivate = false) => GoogleTranslate.BuildUrl(new Uri(url), isPrivate)?.ToString();

    [Fact]
    public void Https_PageGoesThroughTranslateGoog_KeepingPathQueryAndFragment()
    {
        Assert.Equal(
            "https://www-thedailystar-net.translate.goog/news/economy/abc?id=5&_x_tr_sl=auto&_x_tr_tl=bn&_x_tr_hl=bn&_x_tr_pto=wapp#top",
            Build("https://www.thedailystar.net/news/economy/abc?id=5#top"));
    }

    [Fact]
    public void Https_NoQuery_StartsTheQueryWithAQuestionMark()
    {
        Assert.Equal(
            "https://bdnews24-com.translate.goog/?_x_tr_sl=auto&_x_tr_tl=bn&_x_tr_hl=bn&_x_tr_pto=wapp",
            Build("https://bdnews24.com/"));
    }

    [Fact]
    public void Hyphens_AreDoubled_AndRoundTrip()
    {
        Assert.Equal("www-my--site-com", GoogleTranslate.EncodeHost("www.my-site.com"));
        Assert.Equal("www.my-site.com", GoogleTranslate.OriginalHost("www-my--site-com.translate.goog"));
    }

    [Theory]
    [InlineData("www.prothomalo.com")]
    [InlineData("bn.wikipedia.org")]
    [InlineData("my-site.com.bd")]
    [InlineData("a-b-c.d--e.example.org")]
    [InlineData("xn--mgbh0fb.example")]
    public void EncodeThenDecode_GivesTheSameHost(string host)
    {
        var encoded = GoogleTranslate.EncodeHost(host);
        Assert.Equal(host, GoogleTranslate.OriginalHost(encoded + ".translate.goog"));
    }

    [Theory]
    [InlineData("www.example.com")]
    [InlineData("translate.goog")]
    [InlineData(".translate.goog")]
    [InlineData("evil.example.com")]
    public void OriginalHost_OfANormalHost_IsNull(string host) => Assert.Null(GoogleTranslate.OriginalHost(host));

    [Fact]
    public void HttpPage_UsesTheClassicAddress_WithTheAddressEscaped()
    {
        var result = Build("http://old.example.com/a b?x=1&y=2");
        Assert.StartsWith("https://translate.google.com/translate?sl=auto&tl=bn&hl=bn&u=http%3A%2F%2Fold.example.com%2Fa%2520b%3Fx%3D1%26y%3D2", result, StringComparison.Ordinal);
    }

    // ---- Where it is never offered ----

    [Theory]
    [InlineData("https://www.bkash.com/", TranslateRefusal.PaymentSite)]
    [InlineData("https://app.bkash.com/login", TranslateRefusal.PaymentSite)]
    [InlineData("https://www.paypal.com/", TranslateRefusal.PaymentSite)]
    [InlineData("https://www.ebl.com.bd/", TranslateRefusal.PaymentSite)]
    [InlineData("http://localhost:5000/", TranslateRefusal.LocalOrPrivateAddress)]
    [InlineData("http://192.168.1.10/", TranslateRefusal.LocalOrPrivateAddress)]
    [InlineData("https://router.local/", TranslateRefusal.LocalOrPrivateAddress)]
    [InlineData("https://intranet/", TranslateRefusal.LocalOrPrivateAddress)]
    [InlineData("https://93.184.216.34/", TranslateRefusal.UnsupportedAddress)]
    [InlineData("https://www.example.com:8443/", TranslateRefusal.UnsupportedAddress)]
    [InlineData("https://www-example-com.translate.goog/?_x_tr_sl=auto", TranslateRefusal.AlreadyTranslated)]
    [InlineData("https://translate.google.com/translate?u=x", TranslateRefusal.AlreadyTranslated)]
    [InlineData("ftp://example.com/", TranslateRefusal.NotWeb)]
    [InlineData("https://www.thedailystar.net/", TranslateRefusal.None)]
    public void Check_RefusesTheRightPages(string url, TranslateRefusal expected)
    {
        Assert.Equal(expected, GoogleTranslate.Check(new Uri(url), false));
        if (expected != TranslateRefusal.None)
        {
            Assert.Null(GoogleTranslate.BuildUrl(new Uri(url), false));
        }
    }

    [Fact]
    public void PrivateWindow_IsNeverOffered()
    {
        Assert.Equal(TranslateRefusal.PrivateWindow, GoogleTranslate.Check(new Uri("https://www.thedailystar.net/"), true));
        Assert.Null(GoogleTranslate.BuildUrl(new Uri("https://www.thedailystar.net/"), true));
    }

    [Fact]
    public void VeryLongAddress_IsRefused()
    {
        var url = "https://www.example.com/?" + new string('a', GoogleTranslate.MaxAddressLength);
        Assert.Equal(TranslateRefusal.TooLong, GoogleTranslate.Check(new Uri(url), false));
    }

    [Fact]
    public void TranslatedPage_IsAlsoRecognisedByHost()
    {
        Assert.True(GoogleTranslate.IsTranslatedHost("www-example-com.translate.goog"));
        Assert.True(GoogleTranslate.IsTranslatedHost("translate.google.com"));
        Assert.False(GoogleTranslate.IsTranslatedHost("example.com"));
        Assert.False(GoogleTranslate.IsTranslatedHost("nottranslate.goog.example.com"));
    }
}
