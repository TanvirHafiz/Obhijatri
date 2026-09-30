using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Obhijatri.Bangla;

namespace Obhijatri.AI.Scoring;

/// <summary>The words and phrases that scam pages use, loaded once, in the same normalised form as page text.</summary>
internal sealed class ScamPhrases
{
    private static readonly Lazy<ScamPhrases> InstanceLazy = new(Load);

    private ScamPhrases(
        IReadOnlyList<string> urgency, IReadOnlyList<string> prize, IReadOnlyList<string> simKyc,
        IReadOnlyList<string> jobMoney, IReadOnlyList<string> pinOtp,
        IReadOnlyDictionary<string, IReadOnlyList<string>> brandAliases, HashSet<string> abusedTlds)
    {
        Urgency = urgency;
        Prize = prize;
        SimKyc = simKyc;
        JobMoney = jobMoney;
        PinOtp = pinOtp;
        BrandAliases = brandAliases;
        AbusedTlds = abusedTlds;
    }

    public static ScamPhrases Instance => InstanceLazy.Value;

    public IReadOnlyList<string> Urgency { get; }
    public IReadOnlyList<string> Prize { get; }
    public IReadOnlyList<string> SimKyc { get; }
    public IReadOnlyList<string> JobMoney { get; }
    public IReadOnlyList<string> PinOtp { get; }

    /// <summary>Bangla names of brands, by the brand's real domain (normalised).</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> BrandAliases { get; }

    public HashSet<string> AbusedTlds { get; }

    /// <summary>
    /// Text in a form that phrases can be found in: Unicode composed, joiners removed, letters lower
    /// case, Bangla digits as ASCII, and everything that is not a letter, mark or digit as one space.
    /// Text and phrases go through the same function, so they compare fairly.
    /// </summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var composed = text.Normalize(NormalizationForm.FormC);
        var result = new StringBuilder(composed.Length);
        var lastWasSpace = true;
        foreach (var raw in composed)
        {
            if (raw is '‌' or '‍')
            {
                continue;
            }

            var c = BanglaNumerals.ToAsciiDigit(raw);
            var category = char.GetUnicodeCategory(c);
            var keep = char.IsLetterOrDigit(c)
                       || category is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark;
            if (keep)
            {
                result.Append(char.ToLowerInvariant(c));
                lastWasSpace = false;
            }
            else if (!lastWasSpace)
            {
                result.Append(' ');
                lastWasSpace = true;
            }
        }
        return result.ToString().Trim();
    }

    /// <summary>How many different phrases from <paramref name="phrases"/> occur in the normalised text.</summary>
    public static int CountMatches(string normalisedText, IReadOnlyList<string> phrases)
    {
        var count = 0;
        foreach (var phrase in phrases)
        {
            if (normalisedText.Contains(phrase, StringComparison.Ordinal))
            {
                count++;
            }
        }
        return count;
    }

    private static ScamPhrases Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Obhijatri.AI.ScamPhrases.json")
                           ?? throw new InvalidOperationException("Missing scam phrases");
        using var document = JsonDocument.Parse(stream);
        var root = document.RootElement;

        static IReadOnlyList<string> List(JsonElement root, string name) =>
            root.GetProperty(name).EnumerateArray()
                .Select(e => Normalize(e.GetString()))
                .Where(s => s.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToList();

        var aliases = root.GetProperty("brandAliases").EnumerateObject().ToDictionary(
            p => p.Name,
            p => (IReadOnlyList<string>)p.Value.EnumerateArray().Select(e => Normalize(e.GetString())).Where(s => s.Length > 0).ToList(),
            StringComparer.OrdinalIgnoreCase);

        return new ScamPhrases(
            List(root, "urgency"), List(root, "prize"), List(root, "simKyc"), List(root, "jobMoney"), List(root, "pinOtp"),
            aliases,
            new HashSet<string>(root.GetProperty("abusedTlds").EnumerateArray().Select(e => e.GetString()!), StringComparer.OrdinalIgnoreCase));
    }
}
