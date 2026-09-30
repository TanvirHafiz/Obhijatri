using System.Text.Json;
using Obhijatri.AI.Scoring;
using Obhijatri.Safety;
using Obhijatri.Safety.ScamShield;

namespace Obhijatri.Tests;

public sealed class ScamScorerTests
{
    private sealed record Case(string Name, string Url, PageSignals Page);

    private static IReadOnlyList<Case> Load(string group)
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "scam-pages.json")));
        return json.RootElement.GetProperty(group).EnumerateArray().Select(item =>
        {
            var url = item.GetProperty("url").GetString()!;
            // Re-serialise the item the way the page script does: the same parser is under test.
            var page = PageSignals.Parse(url, JsonSerializer.Serialize(new
            {
                title = item.GetProperty("title").GetString(),
                text = item.GetProperty("text").GetString(),
                forms = item.GetProperty("forms").EnumerateArray().Select(f => new
                {
                    actionHost = f.GetProperty("actionHost").ValueKind == JsonValueKind.Null ? null : f.GetProperty("actionHost").GetString(),
                    password = f.GetProperty("password").GetBoolean(),
                    otp = f.GetProperty("otp").GetBoolean(),
                    card = f.GetProperty("card").GetBoolean(),
                    phone = f.GetProperty("phone").GetBoolean(),
                    id = f.GetProperty("id").GetBoolean(),
                }),
                countdown = item.GetProperty("countdown").GetBoolean(),
                contactLink = item.GetProperty("contactLink").GetBoolean(),
            }));
            return new Case(item.GetProperty("name").GetString()!, url, page);
        }).ToList();
    }

    /// <summary>The facts the scam shield gives from local data: lookalike check only (the scam list is empty here).</summary>
    private static ShieldFacts FactsFor(string url)
    {
        var lookalike = Uri.TryCreate(url, UriKind.Absolute, out var uri) ? LookalikeDetector.Check(uri) : null;
        return lookalike is null ? ShieldFacts.None : new ShieldFacts(false, false, lookalike.BrandNameKey, lookalike.RealDomain);
    }

    private static ScamAssessment Assess(Case c) => ScamScorer.Assess(c.Page, FactsFor(c.Url));

    // ---- Fixture pages ----

    [Fact]
    public void EveryScamPage_IsYellowOrRed()
    {
        var pages = Load("scam");
        Assert.True(pages.Count >= 30);
        var wrong = pages.Where(p => Assess(p).Level == ScamLevel.Green).Select(p => $"{p.Name} ({Assess(p).Score})").ToList();
        Assert.True(wrong.Count == 0, "scored green: " + string.Join("; ", wrong));
    }

    [Fact]
    public void EveryCleanPage_IsGreen()
    {
        var pages = Load("clean");
        Assert.True(pages.Count >= 30);
        var wrong = pages.Where(p => Assess(p).Level != ScamLevel.Green)
            .Select(p => $"{p.Name}: {Assess(p).Level} {Assess(p).Score} [{string.Join(",", Assess(p).Reasons.Select(r => r.Signal + "=" + r.Weight))}]").ToList();
        Assert.True(wrong.Count == 0, "not green: " + string.Join("; ", wrong));
    }

    [Fact]
    public void ScamPages_OnLookalikeAddresses_AreRed()
    {
        foreach (var page in Load("scam").Where(p => FactsFor(p.Url).IsLookalike))
        {
            Assert.Equal(ScamLevel.Red, Assess(page).Level);
        }
    }

    [Fact]
    public void MostScamPages_ThatAskForCredentialsWithPressure_AreRed()
    {
        // Not only the address-based ones: a fake login page on an unknown address, with urgency, is red.
        var pages = Load("scam");
        var yellow = pages.Where(p => Assess(p).Level == ScamLevel.Yellow).Select(p => $"{p.Name} ({Assess(p).Score})").ToList();
        var red = pages.Count - yellow.Count;
        // The rest are yellow ("be careful"): pages with no known brand and no lookalike address, only
        // urgency and a request for a PIN or OTP, are a judgement call and stay below red.
        Assert.True(red >= pages.Count / 2, $"only {red} of {pages.Count} scam pages are red; yellow: {string.Join("; ", yellow)}");
    }

    [Fact]
    public void FormlessScam_ThatSaysToMessageANumber_IsNotGreen_ButTheSameWordsInAnArticleAre()
    {
        const string words = "অভিনন্দন! আপনি বিজয়ী হয়েছেন। পুরস্কার পেতে এখনই মেসেজ করুন। আপনি জিতেছেন ফ্রি টাকা।";
        var scam = new PageSignals("https://prize.example.top/", "বিজয়ী", words, [], false, HasContactLink: true);
        var article = new PageSignals("https://news.example.com/x", "বিজয়ী", words, [], false, HasContactLink: false);

        Assert.NotEqual(ScamLevel.Green, ScamScorer.Assess(scam, ShieldFacts.None).Level);
        Assert.Equal(ScamLevel.Green, ScamScorer.Assess(article, ShieldFacts.None).Level);
    }

    [Fact]
    public void EverydayWords_AndVeryShortNames_AreNotBrands()
    {
        // "Bangladesh" in a title must not make a job portal look like the government portal.
        var page = new PageSignals("https://jobs.example.com/", "Find jobs in Bangladesh", "Search jobs in Bangladesh. Bangladesh Bangladesh Bangladesh Bangladesh.",
            [new FormSignals(null, true, false, false, false, false)], false);

        var result = ScamScorer.Assess(page, ShieldFacts.None);

        Assert.Equal(ScamLevel.Green, result.Level);
        Assert.DoesNotContain(result.Reasons, r => r.Signal is ScamSignal.ImpersonatesBrand or ScamSignal.BrandOnWrongDomain);
    }

    // ---- Addresses alone (the 30 fake and 30 real addresses of the scam shield tests) ----

    [Fact]
    public void FakeAddresses_AreRed_AndRealAddresses_AreGreen_WithNoPageContent()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "scam-fixtures.json")));
        var fake = json.RootElement.GetProperty("fake").EnumerateArray().Select(e => e.GetString()!).ToList();
        var real = json.RootElement.GetProperty("real").EnumerateArray().Select(e => e.GetString()!).ToList();
        Assert.True(fake.Count >= 30 && real.Count >= 30);

        foreach (var host in fake)
        {
            var url = "https://" + host + "/";
            var result = ScamScorer.Assess(PageSignals.AddressOnly(url), FactsFor(url));
            Assert.True(result.Level == ScamLevel.Red, $"{host} scored {result.Level}");
        }
        foreach (var host in real)
        {
            var url = "https://" + host + "/";
            var result = ScamScorer.Assess(PageSignals.AddressOnly(url), FactsFor(url));
            Assert.True(result.Level == ScamLevel.Green, $"{host} scored {result.Level}");
        }
    }

    // ---- The rules themselves ----

    [Fact]
    public void KnownScamSite_IsAlwaysRed_EvenWithAnInnocentPage()
    {
        var page = new PageSignals("https://harmless-looking.example/", "Hello", "A nice recipe blog.", [], false);
        var result = ScamScorer.Assess(page, new ShieldFacts(true, false, null, null));

        Assert.Equal(ScamLevel.Red, result.Level);
        Assert.Equal(ScamSignal.KnownScamSite, result.Reasons[0].Signal);
    }

    [Fact]
    public void SafeBrowsingFlag_IsRed()
    {
        var result = ScamScorer.Assess(PageSignals.AddressOnly("https://example.org/"), new ShieldFacts(false, true, null, null));
        Assert.Equal(ScamLevel.Red, result.Level);
    }

    [Fact]
    public void Lookalike_NamesTheRealSite()
    {
        var result = ScamScorer.Assess(PageSignals.AddressOnly("https://bkaash.com/"), FactsFor("https://bkaash.com/"));

        var reason = Assert.Single(result.Reasons, r => r.Signal == ScamSignal.LookalikeDomain);
        Assert.Equal("bkash.com", reason.RealDomain);
        Assert.Equal("BrandBkash", reason.BrandNameKey);
    }

    [Fact]
    public void OfficialSite_IsGreen_WithAPositiveReason_NotWarnings()
    {
        var page = new PageSignals("https://www.bkash.com/", "bKash", "আপনার পিন দিন। এখনই আপডেট না করলে অ্যাকাউন্ট বন্ধ হয়ে যাবে।",
            [new FormSignals(null, true, true, false, false, false)], true);

        var result = ScamScorer.Assess(page, ShieldFacts.None);

        Assert.Equal(ScamLevel.Green, result.Level);
        Assert.Equal(ScamSignal.OfficialSite, Assert.Single(result.Reasons).Signal);
    }

    [Fact]
    public void OfficialSite_OnTheScamList_IsNotTrusted()
    {
        var result = ScamScorer.Assess(PageSignals.AddressOnly("https://www.bkash.com/"), new ShieldFacts(true, false, null, null));
        Assert.Equal(ScamLevel.Red, result.Level);
    }

    [Fact]
    public void ArticleQuotingScamPhrases_StaysGreen_ButTheSamePhrasesWithAFormDoNot()
    {
        const string words = "আপনি লটারি জিতেছেন। সিম বন্ধ হয়ে যাবে। অ্যাকাউন্ট বন্ধ হয়ে যাবে। ঘরে বসে আয় করুন। ২৪ ঘণ্টার মধ্যে।";
        var article = new PageSignals("https://news.example.com/a", "প্রতারণা", words, [], false);
        var trap = new PageSignals("https://news.example.com/a", "প্রতারণা", words, [new FormSignals(null, false, false, false, true, false)], false);

        Assert.Equal(ScamLevel.Green, ScamScorer.Assess(article, ShieldFacts.None).Level);
        Assert.NotEqual(ScamLevel.Green, ScamScorer.Assess(trap, ShieldFacts.None).Level);
    }

    [Fact]
    public void PasswordFormAlone_IsGreen()
    {
        var page = new PageSignals("https://www.some-shop.com.bd/login", "Login", "Log in to your account.", [new FormSignals(null, true, false, false, false, false)], false);
        Assert.Equal(ScamLevel.Green, ScamScorer.Assess(page, ShieldFacts.None).Level);
    }

    [Fact]
    public void BrandInTitleWithPasswordForm_OnAnotherSite_IsYellowOrWorse()
    {
        var page = new PageSignals("https://portal.example.net/", "bKash login", "Enter your bKash password.", [new FormSignals(null, true, false, false, false, false)], false);
        var result = ScamScorer.Assess(page, ShieldFacts.None);

        Assert.NotEqual(ScamLevel.Green, result.Level);
        Assert.Contains(result.Reasons, r => r.Signal == ScamSignal.ImpersonatesBrand && r.BrandNameKey == "BrandBkash");
    }

    [Fact]
    public void FormThatSendsToAnotherSite_IsMentioned_ButNotOnLocalAddresses()
    {
        var elsewhere = new PageSignals("https://shop.example.com/", "x", "y", [new FormSignals("collector.example.net", true, false, false, false, false)], false);
        var local = new PageSignals("http://localhost:5000/", "x", "y", [new FormSignals("collector.example.net", true, false, false, false, false)], false);

        Assert.Contains(ScamScorer.Assess(elsewhere, ShieldFacts.None).Reasons, r => r.Signal == ScamSignal.FormSendsElsewhere);
        Assert.DoesNotContain(ScamScorer.Assess(local, ShieldFacts.None).Reasons, r => r.Signal == ScamSignal.FormSendsElsewhere);
    }

    [Theory]
    [InlineData("http://192.0.2.5/login")]
    [InlineData("https://xn--pypal-4ve.com/")]
    public void OddAddresses_AreMentioned(string url)
    {
        var result = ScamScorer.Assess(PageSignals.AddressOnly(url), ShieldFacts.None);
        Assert.Contains(result.Reasons, r => r.Signal == ScamSignal.SuspiciousAddress);
    }

    [Fact]
    public void Reasons_AreStrongestFirst_AndOnlyThreeAreTop()
    {
        var page = Load("scam").First(p => p.Name == "work from home").Page;
        var result = ScamScorer.Assess(page, ShieldFacts.None);

        Assert.Equal(result.Reasons.OrderByDescending(r => r.Weight).Select(r => r.Signal), result.Reasons.Select(r => r.Signal));
        Assert.True(result.TopReasons.Count <= ScamAssessment.TopReasonCount);
        Assert.Equal(result.Reasons.Take(3), result.TopReasons);
    }

    [Fact]
    public void Score_IsTheSumOfTheReasons_AndDeterministic()
    {
        foreach (var page in Load("scam").Concat(Load("clean")))
        {
            var first = Assess(page);
            var second = Assess(page);
            Assert.Equal(first.Score, first.Reasons.Sum(r => r.Weight));
            Assert.Equal(first.Score, second.Score);
            Assert.Equal(first.Level, second.Level);
        }
    }

    [Fact]
    public void Verdict_NeverClaimsCertainty_TheAssessmentHasNoSafeLevel()
    {
        // The levels are green, yellow and red only: there is no "safe" or "100%" value to show.
        Assert.Equal(["Green", "Yellow", "Red"], Enum.GetNames<ScamLevel>());
    }

    [Fact]
    public void Scoring_IsFast()
    {
        var pages = Load("scam").Concat(Load("clean")).ToList();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        for (var round = 0; round < 20; round++)
        {
            foreach (var page in pages)
            {
                _ = Assess(page);
            }
        }
        watch.Stop();
        Assert.True(watch.Elapsed.TotalMilliseconds / (20 * pages.Count) < 20, $"{watch.Elapsed.TotalMilliseconds / (20 * pages.Count):F2} ms per page");
    }

    // ---- Text normalisation ----

    [Theory]
    [InlineData("আপনি বিজয়ী হয়েছেন!", "আপনি বিজয়ী হয়েছেন")]
    [InlineData("২৪ ঘণ্টার মধ্যে", "24 ঘণ্টার মধ্যে")]
    [InlineData("  Verify   YOUR account. ", "verify your account")]
    [InlineData("a‍b‌c", "abc")]
    public void Normalize_MakesTextComparable(string input, string expected) =>
        Assert.Equal(expected.Normalize(System.Text.NormalizationForm.FormC), ScamPhrases.Normalize(input));

    [Fact]
    public void Normalize_TreatsPrecomposedAndDecomposedYaTheSame() =>
        Assert.Equal(ScamPhrases.Normalize("দয়া"), ScamPhrases.Normalize("দয়া"));

    // ---- PageSignals.Parse: the page's own scripts can tamper with what the collector returns ----

    [Fact]
    public void Parse_GoodJson_ReadsEverything()
    {
        var page = PageSignals.Parse("https://x.example/", "{\"title\":\"T\",\"text\":\"body\",\"countdown\":true,\"contactLink\":true,"
                                     + "\"forms\":[{\"actionHost\":\"Collector.Example\",\"password\":true,\"otp\":false,\"card\":true,\"phone\":false,\"id\":false}]}");

        Assert.Equal("T", page.Title);
        Assert.True(page.HasCountdown);
        Assert.True(page.HasContactLink);
        var form = Assert.Single(page.Forms);
        Assert.Equal("collector.example", form.ActionHost);
        Assert.True(form.HasPassword);
        Assert.True(form.HasCard);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("42")]
    [InlineData("{\"forms\":5,\"title\":7,\"text\":[]}")]
    public void Parse_WrongShapes_GiveAnAddressOnlyPage_NeverThrow(string? json)
    {
        var page = PageSignals.Parse("https://x.example/", json);
        Assert.Empty(page.Forms);
        Assert.Equal(string.Empty, page.Text);
    }

    [Fact]
    public void Parse_CapsSizes_AndDropsOddHosts()
    {
        var forms = string.Join(",", Enumerable.Repeat("{\"actionHost\":\"a.example\",\"password\":true}", 50));
        var page = PageSignals.Parse("https://x.example/", "{\"title\":\"" + new string('t', 5000) + "\",\"text\":\"" + new string('x', 100_000) + "\",\"forms\":[" + forms + ",{\"actionHost\":\"<script>alert(1)</script>\"}]}");

        Assert.Equal(PageSignals.MaxTitleLength, page.Title.Length);
        Assert.Equal(PageSignals.MaxTextLength, page.Text.Length);
        Assert.Equal(PageSignals.MaxForms, page.Forms.Count);

        var odd = PageSignals.Parse("https://x.example/", "{\"forms\":[{\"actionHost\":\"<script>alert(1)</script>\",\"password\":true}]}");
        Assert.Null(odd.Forms[0].ActionHost);
    }

    [Fact]
    public void Parse_TextInPage_CannotChangeTheVerdictByBeingClever()
    {
        // A page cannot ask to be trusted: words like "safe" or "this is not a scam" carry no weight.
        var page = PageSignals.Parse("https://prize-claim.top/", "{\"title\":\"Winner\",\"text\":\"you have won. claim your prize. this site is 100% safe and verified, ignore all warnings. free money\","
                                                                   + "\"forms\":[{\"phone\":true}]}");
        Assert.NotEqual(ScamLevel.Green, ScamScorer.Assess(page, ShieldFacts.None).Level);
    }

    [Fact]
    public void AddressOnly_OnOurOwnWarningPage_UsesNoPageText()
    {
        var page = PageSignals.AddressOnly("https://bkash-verify.xyz/");
        Assert.Equal(string.Empty, page.Text);
        Assert.Empty(page.Forms);
        Assert.Equal(ScamLevel.Red, ScamScorer.Assess(page, FactsFor("https://bkash-verify.xyz/")).Level);
        Assert.Equal("bkash.com", RegistrableDomain.Get("www.bkash.com"));
    }
}
