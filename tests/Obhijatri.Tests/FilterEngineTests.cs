using Obhijatri.Safety;
using Obhijatri.Safety.Filtering;

namespace Obhijatri.Tests;

public sealed class FilterEngineTests
{
    private static FilterEngine Engine(params string[] rules) => FilterEngine.Build(rules);

    private static bool Blocked(FilterEngine engine, string url, string page = "www.prothomalo.com", RequestType type = RequestType.Script) =>
        engine.Check(url, new Uri(url).Host, page, type).IsBlocked;

    [Fact]
    public void DomainRule_BlocksDomainAndSubdomains_Only()
    {
        var e = Engine("||doubleclick.net^");
        Assert.True(Blocked(e, "https://doubleclick.net/x.js"));
        Assert.True(Blocked(e, "https://ad.g.doubleclick.net/pagead/ads?x=1"));
        Assert.False(Blocked(e, "https://notdoubleclick.net/x.js"));
        Assert.False(Blocked(e, "https://doubleclick.net.example.com/x.js"));
        Assert.Equal(1, e.DomainRuleCount);
    }

    [Fact]
    public void PatternRules_WithWildcardsAnchorsAndSeparator()
    {
        var e = Engine("/banner/*/ad_", "|https://ads.", ".swf|", "||cdn.example.com/ads^");
        Assert.True(Blocked(e, "https://site.com/banner/300x250/ad_1.png"));
        Assert.False(Blocked(e, "https://site.com/banner/ad.png"));
        Assert.True(Blocked(e, "https://ads.site.com/a.js"));
        Assert.False(Blocked(e, "https://site.com/?u=https://ads.x"));
        Assert.True(Blocked(e, "https://site.com/movie.swf"));
        Assert.False(Blocked(e, "https://site.com/movie.swf?x"));
        Assert.True(Blocked(e, "https://cdn.example.com/ads/1.js"));
        Assert.True(Blocked(e, "https://cdn.example.com/ads?x"));
        Assert.False(Blocked(e, "https://cdn.example.com/adsense.js"));
    }

    [Fact]
    public void ThirdPartyOption_UsesTheSite_NotTheExactHost()
    {
        var e = Engine("||tracker.example^$third-party");
        Assert.True(Blocked(e, "https://tracker.example/p.gif", page: "www.prothomalo.com"));
        Assert.False(Blocked(e, "https://tracker.example/p.gif", page: "www.tracker.example"));
        Assert.False(Blocked(e, "https://cdn.tracker.example/p.gif", page: "tracker.example"));
    }

    [Fact]
    public void TypeOptions_IncludeAndExclude()
    {
        var e = Engine("||ads.example^$script,image", "/pixel.$~image");
        Assert.True(Blocked(e, "https://ads.example/a.js", type: RequestType.Script));
        Assert.False(Blocked(e, "https://ads.example/a.css", type: RequestType.Stylesheet));
        Assert.True(Blocked(e, "https://site.com/pixel.js", type: RequestType.Script));
        Assert.False(Blocked(e, "https://site.com/pixel.gif", type: RequestType.Image));
    }

    [Fact]
    public void DomainOption_LimitsToPages()
    {
        var e = Engine("/sponsor/*$domain=prothomalo.com|~blog.prothomalo.com");
        Assert.True(Blocked(e, "https://img.site/sponsor/1.png", page: "www.prothomalo.com"));
        Assert.False(Blocked(e, "https://img.site/sponsor/1.png", page: "blog.prothomalo.com"));
        Assert.False(Blocked(e, "https://img.site/sponsor/1.png", page: "www.jugantor.com"));
    }

    [Fact]
    public void Exceptions_OverrideBlocks_UnlessImportant()
    {
        var e = Engine("||ads.example^", "@@||ads.example/allowed/", "||evil.example^$important", "@@||evil.example^");
        Assert.True(Blocked(e, "https://ads.example/x.js"));
        Assert.False(Blocked(e, "https://ads.example/allowed/x.js"));
        Assert.True(Blocked(e, "https://evil.example/x.js"));
    }

    [Fact]
    public void PlainDomainException_AllowsSubtree()
    {
        var e = Engine("/ads.js", "@@||good.example^");
        Assert.True(Blocked(e, "https://other.example/ads.js"));
        Assert.False(Blocked(e, "https://cdn.good.example/ads.js"));
    }

    [Fact]
    public void DocumentException_TurnsOffFilteringForAPage()
    {
        var e = Engine("||ads.example^", "@@||trusted.example^$document");
        Assert.True(e.IsPageExcepted("https://www.trusted.example/page"));
        Assert.False(e.IsPageExcepted("https://www.prothomalo.com/"));
    }

    [Theory]
    [InlineData("! comment")]
    [InlineData("[Adblock Plus 2.0]")]
    [InlineData("example.com##.ad-banner")]
    [InlineData("example.com#@#.ad")]
    [InlineData("/^https?:\\/\\/ads/")]
    [InlineData("||popunder.example^$popup")]
    [InlineData("||x.example^$redirect=noop.js")]
    [InlineData("||x.example^$csp=script-src 'none'")]
    [InlineData("||x.example^$document")]
    [InlineData("")]
    public void UnsupportedOrNonNetworkLines_AreSkipped(string line)
    {
        Assert.Null(FilterRule.Parse(line));
    }

    [Fact]
    public void MatchCase_IsRespected()
    {
        var e = Engine("/BannerAd.$match-case");
        Assert.True(Blocked(e, "https://site.com/BannerAd.js"));
        Assert.False(Blocked(e, "https://site.com/bannerad.js"));
    }

    [Theory]
    [InlineData("||ads.example.com^", "example")]
    [InlineData("/banner/*/ad_", "banner")]
    [InlineData("-advert-", "advert")]
    [InlineData("*/pagead/*", "pagead")]
    [InlineData("ad*", null)]
    public void Tokens_AreWholeWords(string pattern, string? expected)
    {
        Assert.Equal(expected, FilterEngine.BestToken(pattern));
    }

    [Theory]
    [InlineData("www.prothomalo.com", "prothomalo.com")]
    [InlineData("images.prothomalo.com", "prothomalo.com")]
    [InlineData("www.bangladesh.gov.bd", "bangladesh.gov.bd")]
    [InlineData("shop.daraz.com.bd", "daraz.com.bd")]
    [InlineData("bbc.co.uk", "bbc.co.uk")]
    [InlineData("localhost", "localhost")]
    [InlineData("192.168.0.1", "192.168.0.1")]
    public void RegistrableDomain_GroupsSubdomains(string host, string site)
    {
        Assert.Equal(site, RegistrableDomain.Get(host));
    }

    [Fact]
    public void BangladeshExtras_BlockTheMoMagicAdRenderer_Only()
    {
        var e = FilterEngine.Build(File.ReadLines(Path.Combine(AppContext.BaseDirectory, "Filters", "bd-extra.txt")));
        Assert.True(e.RuleCount >= 1);
        Assert.True(Blocked(e, "https://cdn.momagic.com/publisher/5758b893/truereachAdRender.js", page: "www.jugantor.com"));
        Assert.False(Blocked(e, "https://www.momagic.com/about", page: "www.jugantor.com", type: RequestType.Subdocument));
    }

    [Fact]
    public void EmptyEngine_AllowsEverything()
    {
        Assert.False(Blocked(FilterEngine.Empty, "https://doubleclick.net/x.js"));
    }
}
