using System.Text;

namespace Obhijatri.Bangla.Phonetic;

/// <summary>
/// A forgiving "sounds like" key used to match what someone typed in English letters against
/// Bangla words, without caring how exactly they spelled it. For example "tomar", "tOmar" and
/// তোমার all give the same key, and so do "dhaka", "Dhaka" and ঢাকা.
///
/// Both sides are reduced to rough English letters, then aspiration (kh/k), sibilants (sh/s/S),
/// retroflex and dental pairs (T/t, D/d), long and short vowels, the inherent vowel "o",
/// y/w phala, and doubled letters are all merged.
/// </summary>
public static class LooseKey
{
    private static readonly (string From, string To)[] Digraphs =
    [
        ("kkh", "k"), ("ksh", "k"), ("kh", "k"), ("gh", "g"), ("ch", "c"), ("jh", "j"), ("th", "t"),
        ("dh", "d"), ("ph", "p"), ("bh", "b"), ("sh", "s"), ("rh", "r"), ("ng", "n"), ("ee", "i"), ("oo", "u"),
    ];

    private static readonly Dictionary<char, string> BanglaToRoman = new()
    {
        ['অ'] = "o", ['আ'] = "a", ['ই'] = "i", ['ঈ'] = "i", ['উ'] = "u", ['ঊ'] = "u", ['ঋ'] = "ri",
        ['এ'] = "e", ['ঐ'] = "oi", ['ও'] = "o", ['ঔ'] = "ou",
        ['া'] = "a", ['ি'] = "i", ['ী'] = "i", ['ু'] = "u", ['ূ'] = "u", ['ৃ'] = "ri", ['ে'] = "e",
        ['ৈ'] = "oi", ['ো'] = "o", ['ৌ'] = "ou",
        ['ক'] = "k", ['খ'] = "kh", ['গ'] = "g", ['ঘ'] = "gh", ['ঙ'] = "ng", ['চ'] = "c", ['ছ'] = "ch",
        ['জ'] = "j", ['ঝ'] = "jh", ['ঞ'] = "n", ['ট'] = "t", ['ঠ'] = "th", ['ড'] = "d", ['ঢ'] = "dh",
        ['ণ'] = "n", ['ত'] = "t", ['থ'] = "th", ['দ'] = "d", ['ধ'] = "dh", ['ন'] = "n", ['প'] = "p",
        ['ফ'] = "ph", ['ব'] = "b", ['ভ'] = "bh", ['ম'] = "m", ['য'] = "j", ['র'] = "r", ['ল'] = "l",
        ['শ'] = "sh", ['ষ'] = "sh", ['স'] = "s", ['হ'] = "h",
        ['\u09DC'] = "r", ['\u09DD'] = "rh", ['\u09DF'] = "y", // ড় ঢ় য় (precomposed)
        ['ৎ'] = "t", ['ং'] = "ng",
    };

    private const char Hasanta = '্';
    private const char Nukta = '়';

    public static string FromRoman(string roman) => Normalize(roman);

    public static string FromBangla(string bangla)
    {
        var rough = new StringBuilder(bangla.Length * 2);
        var text = bangla.Normalize(NormalizationForm.FormC);
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == Hasanta)
            {
                // "ya" and "ba" phala (্য, ্ব) are not pronounced as separate letters.
                if (i + 1 < text.Length && text[i + 1] is 'য' or 'ব')
                {
                    i++;
                }
                continue;
            }

            // Decomposed ড় / ঢ় / য় (letter + nukta).
            if (i + 1 < text.Length && text[i + 1] == Nukta)
            {
                rough.Append(c switch { 'ড' => "r", 'ঢ' => "rh", 'য' => "y", _ => string.Empty });
                i++;
                continue;
            }

            if (BanglaToRoman.TryGetValue(c, out var roman))
            {
                rough.Append(roman);
            }
        }
        return Normalize(rough.ToString());
    }

    private static string Normalize(string input)
    {
        var text = input.ToLowerInvariant();
        foreach (var (from, to) in Digraphs)
        {
            text = text.Replace(from, to, StringComparison.Ordinal);
        }

        var key = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            var mapped = c switch
            {
                'v' => 'b',
                'f' => 'p',
                'z' => 'j',
                'q' => 'k',
                'x' => 's',
                // The inherent vowel, y and w phala and silent letters carry little information.
                'o' or 'y' or 'w' => '\0',
                >= 'a' and <= 'z' => c,
                _ => '\0',
            };
            // Doubled letters (tt, kk, aa) count once.
            if (mapped != '\0' && (key.Length == 0 || key[^1] != mapped))
            {
                key.Append(mapped);
            }
        }
        return key.ToString();
    }
}
