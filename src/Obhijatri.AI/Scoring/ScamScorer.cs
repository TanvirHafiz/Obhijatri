using System.Net;
using Obhijatri.Safety;
using Obhijatri.Safety.ScamShield;

namespace Obhijatri.AI.Scoring;

/// <summary>
/// Layer 1 of "এটা কি প্রতারণা?": a rule based judgement that works with no network and no model. It
/// weighs the address (the scam shield's facts and how the address looks), what the page asks for
/// (passwords, PINs, OTPs, card numbers, where the answers go) and what it says (urgency, prizes,
/// SIM and KYC threats, money offers, countdowns, brand names). The result is an estimate: it never
/// says a page is safe, only that no big warning sign was found.
/// </summary>
public static class ScamScorer
{
    public const int YellowFrom = 25;
    public const int RedFrom = 50;

    // Weights. A single weak sign stays below yellow; signs that go together add up.
    private const int KnownScamWeight = 100;
    private const int LookalikeWeight = 70;
    private const int ImpersonationStrong = 40;
    private const int ImpersonationWeak = 12;

    /// <summary>A brand named this often (or in the title) is what the page is about, not just a payment option.</summary>
    private const int StrongMentionCount = 5;
    private const int BrandMentionStrong = 10;
    private const int BrandMentionWeak = 6;
    private const int PinOtpFormWeight = 12;
    private const int CardFormWeight = 8;
    private const int FormElsewhereWeight = 15;
    private const int NoHttpsWeight = 8;
    private const int CountdownAlone = 6;
    private const int CountdownWithText = 12;
    private const int AddressMax = 20;

    private const int MinBrandTokenLength = 3;

    private static readonly HashSet<string> EverydayWords = new(StringComparer.Ordinal) { "bangladesh" };

    /// <summary>Pages that ask for nothing are usually articles; even one about scams may quote scam phrases.</summary>
    private const int TextOnlyCap = 20;

    public static ScamAssessment Assess(PageSignals page, ShieldFacts facts)
    {
        var reasons = new List<ScamReason>();
        void Add(ScamSignal signal, int weight, string? brandKey = null, string? realDomain = null) =>
            reasons.Add(new ScamReason(signal, weight, brandKey, realDomain));

        var host = HostOf(page.Url, out var isHttps);
        var local = host.Length == 0 || HttpsUpgrade.IsLocalOrPrivate(host);

        if (facts.IsKnownScam)
        {
            Add(ScamSignal.KnownScamSite, KnownScamWeight);
        }
        if (facts.IsSafeBrowsingFlagged)
        {
            Add(ScamSignal.SafeBrowsingFlag, KnownScamWeight);
        }
        if (facts.IsLookalike)
        {
            Add(ScamSignal.LookalikeDomain, LookalikeWeight, facts.LookalikeBrandKey, facts.LookalikeRealDomain);
        }

        // The real site of a protected brand, and nothing on the scam list says otherwise.
        var site = RegistrableDomain.Get(host);
        var official = ProtectedBrands.All.FirstOrDefault(b => b.Domain == site);
        if (reasons.Count == 0 && official is not null)
        {
            return new ScamAssessment(ScamLevel.Green, 0, [new ScamReason(ScamSignal.OfficialSite, 0, official.NameKey)]);
        }

        if (!local && AddressWeight(host) is > 0 and var addressWeight)
        {
            Add(ScamSignal.SuspiciousAddress, addressWeight);
        }

        var phrases = ScamPhrases.Instance;
        var text = ScamPhrases.Normalize(page.Title + " " + page.Text);
        var forms = page.Forms;
        var asksSensitive = forms.Any(f => f.AsksSensitive);
        var asksCredentials = forms.Any(f => f.HasPassword || f.HasOtpOrPin);

        // A page can also draw people in without a form: "message us on WhatsApp on this number".
        var collects = forms.Any(f => f.CollectsPersonalData) || page.HasContactLink;
        var textReasons = new List<ScamReason>();

        // A brand's name on a page that is not the brand's own site. A shop that lists "pay with bKash"
        // next to a card form is normal, so only a password, PIN or OTP form under a brand's name counts
        // as impersonation.
        if (official is null && FindBrand(text, page.Title) is { } brand)
        {
            if (asksCredentials)
            {
                Add(ScamSignal.ImpersonatesBrand, brand.Strong ? ImpersonationStrong : ImpersonationWeak, brand.Brand.NameKey);
            }
            else
            {
                // Only a mention. Like the phrases below it counts for little on a page that asks for nothing.
                textReasons.Add(new ScamReason(ScamSignal.BrandOnWrongDomain, brand.Strong ? BrandMentionStrong : BrandMentionWeak, brand.Brand.NameKey));
            }
        }

        // What the forms ask for.
        var pinOtpFromForm = forms.Any(f => f.HasOtpOrPin);
        if (pinOtpFromForm)
        {
            Add(ScamSignal.PinOrOtpRequest, PinOtpFormWeight);
        }
        if (forms.Any(f => f.HasCard))
        {
            Add(ScamSignal.CardDetailsRequest, CardFormWeight);
        }
        if (!local && forms.Any(f => f.CollectsPersonalData && f.ActionHost is { } target && RegistrableDomain.IsThirdParty(target, host)))
        {
            Add(ScamSignal.FormSendsElsewhere, FormElsewhereWeight);
        }
        if (!local && !isHttps && asksSensitive)
        {
            Add(ScamSignal.NoHttps, NoHttpsWeight);
        }

        // What the words say.
        AddText(textReasons, ScamSignal.UrgencyWords, ScamPhrases.CountMatches(text, phrases.Urgency), 8, 24);
        AddText(textReasons, ScamSignal.PrizeClaim, ScamPhrases.CountMatches(text, phrases.Prize), 20, 40);
        AddText(textReasons, ScamSignal.SimOrKycThreat, ScamPhrases.CountMatches(text, phrases.SimKyc), 18, 36);
        AddText(textReasons, ScamSignal.JobOrMoneyOffer, ScamPhrases.CountMatches(text, phrases.JobMoney), 10, 20);
        if (!pinOtpFromForm)
        {
            AddText(textReasons, ScamSignal.PinOrOtpRequest, ScamPhrases.CountMatches(text, phrases.PinOtp), 10, 20);
        }

        // A page that asks for nothing is most likely an article; do not let quoted phrases make it yellow.
        if (!collects)
        {
            var total = textReasons.Sum(r => r.Weight);
            if (total > TextOnlyCap)
            {
                textReasons = textReasons.Select(r => r with { Weight = Math.Max(1, r.Weight * TextOnlyCap / total) }).ToList();
            }
        }
        reasons.AddRange(textReasons);

        if (page.HasCountdown)
        {
            Add(ScamSignal.CountdownTimer, textReasons.Count > 0 ? CountdownWithText : CountdownAlone);
        }

        var score = reasons.Sum(r => r.Weight);
        var ordered = reasons.OrderByDescending(r => r.Weight).ToList();
        var level = facts.IsKnownScam || facts.IsSafeBrowsingFlagged || facts.IsLookalike || score >= RedFrom ? ScamLevel.Red
            : score >= YellowFrom ? ScamLevel.Yellow
            : ScamLevel.Green;
        return new ScamAssessment(level, score, ordered);
    }

    private static void AddText(List<ScamReason> reasons, ScamSignal signal, int matches, int each, int max)
    {
        if (matches > 0)
        {
            reasons.Add(new ScamReason(signal, Math.Min(matches * each, max)));
        }
    }

    // ---- Brand names ----

    private sealed record BrandMention(ProtectedBrand Brand, bool Strong);

    /// <summary>The protected brand the page talks about most, and whether it is in the title or named often.</summary>
    private static BrandMention? FindBrand(string text, string title)
    {
        var phrases = ScamPhrases.Instance;
        var padded = " " + text + " ";
        var paddedTitle = " " + ScamPhrases.Normalize(title) + " ";

        BrandMention? best = null;
        var bestCount = 0;
        foreach (var brand in ProtectedBrands.All)
        {
            var count = 0;
            var inTitle = false;

            // Short or everyday names ("bb", "jb", "bangladesh") say nothing about a brand; their
            // Bangla and full names below still count.
            var name = ProtectedBrands.Token(brand);
            if (name.Length >= MinBrandTokenLength && !EverydayWords.Contains(name))
            {
                var token = " " + name + " ";
                count = CountOccurrences(padded, token);
                inTitle = paddedTitle.Contains(token, StringComparison.Ordinal);
            }

            // Bangla names take suffixes (বিকাশে, নগদের), so they match as the start of a word.
            if (phrases.BrandAliases.TryGetValue(brand.Domain, out var aliases))
            {
                foreach (var alias in aliases)
                {
                    count += CountOccurrences(padded, " " + alias);
                    inTitle |= paddedTitle.Contains(" " + alias, StringComparison.Ordinal);
                }
            }

            if (count > bestCount)
            {
                best = new BrandMention(brand, inTitle || count >= StrongMentionCount);
                bestCount = count;
            }
        }
        return best;
    }

    private static int CountOccurrences(string text, string needle)
    {
        var count = 0;
        for (var index = text.IndexOf(needle, StringComparison.Ordinal); index >= 0; index = text.IndexOf(needle, index + 1, StringComparison.Ordinal))
        {
            count++;
        }
        return count;
    }

    // ---- The address ----

    private static string HostOf(string url, out bool isHttps)
    {
        isHttps = false;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return string.Empty;
        }
        isHttps = uri.Scheme == Uri.UriSchemeHttps;
        return uri.Host.ToLowerInvariant();
    }

    /// <summary>How odd the address itself looks: a raw IP, punycode, an abused ending, many hyphens or parts.</summary>
    private static int AddressWeight(string host)
    {
        if (host.Length == 0)
        {
            return 0;
        }

        var weight = 0;
        if (IPAddress.TryParse(host.Trim('[', ']'), out _))
        {
            weight += 15;
        }
        var labels = host.Split('.');
        if (labels.Any(l => l.StartsWith("xn--", StringComparison.Ordinal)))
        {
            weight += 15;
        }
        if (labels.Length > 1 && ScamPhrases.Instance.AbusedTlds.Contains(labels[^1]))
        {
            weight += 8;
        }
        if (host.Count(c => c == '-') >= 3)
        {
            weight += 6;
        }
        if (labels.Length >= 5)
        {
            weight += 6;
        }
        return Math.Min(weight, AddressMax);
    }
}
