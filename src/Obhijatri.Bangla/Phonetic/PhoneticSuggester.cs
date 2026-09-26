namespace Obhijatri.Bangla.Phonetic;

/// <summary>
/// Suggestions for a word typed in English letters: the exact Avro result first, then common words
/// that sound the same (so "tomar" also offers তোমার), then common words that start the same way.
/// </summary>
public sealed class PhoneticSuggester
{
    public const int DefaultMax = 6;
    private const int MinPrefixKeyLength = 3;

    private static readonly Lazy<PhoneticSuggester> Shared = new(() => new PhoneticSuggester(
        AvroPhonetic.Instance,
        AvroPhonetic.ReadResource("Obhijatri.Bangla.BanglaWords.txt")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)));

    private readonly AvroPhonetic _avro;
    private readonly (string Word, string Key)[] _words;

    public PhoneticSuggester(AvroPhonetic avro, IEnumerable<string> words)
    {
        _avro = avro;
        // Keep the list order (most common first) and drop duplicates.
        _words = words.Distinct(StringComparer.Ordinal).Select(w => (w, LooseKey.FromBangla(w))).ToArray();
    }

    /// <summary>The engine with the bundled word list.</summary>
    public static PhoneticSuggester Instance => Shared.Value;

    public int WordCount => _words.Length;

    public IReadOnlyList<string> Suggest(string roman, int max = DefaultMax)
    {
        var typed = roman.Trim();
        if (typed.Length == 0 || max <= 0)
        {
            return [];
        }

        var results = new List<string>(max) { _avro.Convert(typed) };
        var key = LooseKey.FromRoman(typed);
        if (key.Length == 0)
        {
            return results;
        }

        foreach (var (word, wordKey) in _words)
        {
            if (results.Count >= max)
            {
                return results;
            }
            if (wordKey == key && !results.Contains(word))
            {
                results.Add(word);
            }
        }

        if (key.Length >= MinPrefixKeyLength)
        {
            foreach (var (word, wordKey) in _words)
            {
                if (results.Count >= max)
                {
                    break;
                }
                if (wordKey.Length > key.Length && wordKey.StartsWith(key, StringComparison.Ordinal) && !results.Contains(word))
                {
                    results.Add(word);
                }
            }
        }

        return results;
    }
}
