using System.Diagnostics;
using System.Text.Json;
using Obhijatri.Safety.ScamShield;

namespace Obhijatri.Tests;

public class LookalikeDetectorTests
{
    private sealed record Fixtures(string[] Fake, string[] Real);

    private static Fixtures Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "scam-fixtures.json");
        var json = File.ReadAllText(path);
        var doc = JsonDocument.Parse(json).RootElement;
        return new Fixtures(
            doc.GetProperty("fake").EnumerateArray().Select(e => e.GetString()!).ToArray(),
            doc.GetProperty("real").EnumerateArray().Select(e => e.GetString()!).ToArray());
    }

    [Fact]
    public void EveryFakeFixtureIsFlagged()
    {
        var fixtures = Load();
        var missed = fixtures.Fake.Where(host => LookalikeDetector.Check(new Uri("https://" + host)) is null).ToList();
        Assert.True(missed.Count == 0, "Not flagged: " + string.Join(", ", missed));
    }

    [Fact]
    public void NoRealFixtureIsFlagged()
    {
        var fixtures = Load();
        var falsePositives = fixtures.Real.Where(host => LookalikeDetector.Check(new Uri("https://" + host)) is not null).ToList();
        Assert.True(falsePositives.Count == 0, "Flagged real site: " + string.Join(", ", falsePositives));
    }

    [Fact]
    public void FixturesHaveThirtyOfEach()
    {
        var fixtures = Load();
        Assert.Equal(30, fixtures.Fake.Length);
        Assert.Equal(30, fixtures.Real.Length);
    }

    [Theory]
    [InlineData("bkash.com")]
    [InlineData("pay.bkash.com")]
    [InlineData("www.facebook.com")]
    [InlineData("accounts.google.com")]
    [InlineData("smile.amazon.com")]
    [InlineData("prothomalo.com")]
    [InlineData("bdnews24.com")]
    [InlineData("en.wikipedia.org")]
    public void RealSubdomainsAndUnrelatedSitesAreSafe(string host)
    {
        Assert.Null(LookalikeDetector.Check(new Uri("https://" + host)));
    }

    [Fact]
    public void UnrelatedSiteSharingAShortSyllableIsNotFlagged()
    {
        // "brac.net" (the NGO) shares "brac" with BRAC Bank's domain, but is not itself a
        // lookalike of bracbank.com: the brand's token is the bank's own domain label.
        Assert.Null(LookalikeDetector.Check(new Uri("https://brac.net")));
    }

    [Fact]
    public void CheckStaysUnderTwentyMillisecondsOnAverage()
    {
        var fixtures = Load();
        var hosts = fixtures.Fake.Concat(fixtures.Real).Select(h => new Uri("https://" + h)).ToArray();

        // Warm up (first call pays for JIT and the static brand list).
        foreach (var uri in hosts)
        {
            LookalikeDetector.Check(uri);
        }

        var sw = Stopwatch.StartNew();
        const int rounds = 50;
        for (var i = 0; i < rounds; i++)
        {
            foreach (var uri in hosts)
            {
                LookalikeDetector.Check(uri);
            }
        }
        sw.Stop();

        var perCheckMs = sw.Elapsed.TotalMilliseconds / (rounds * hosts.Length);
        Assert.True(perCheckMs < 20, $"Average check took {perCheckMs:F4} ms");
    }
}
