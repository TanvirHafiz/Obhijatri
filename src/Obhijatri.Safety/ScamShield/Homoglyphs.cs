using System.Text;

namespace Obhijatri.Safety.ScamShield;

/// <summary>
/// Turns a host label into a plain-ASCII "skeleton" by mapping characters that look like a Latin
/// letter to that letter: Cyrillic and Greek look-alikes (the classic punycode homoglyph trick,
/// for example Cyrillic "о" instead of "o"), and digits used as letters in scam domains
/// (g00gle, faceb00k). Two labels that are visually confusable end up with the same skeleton.
/// </summary>
public static class Homoglyphs
{
    private static readonly Dictionary<char, char> Map = new()
    {
        // Cyrillic look-alikes.
        ['а'] = 'a', ['е'] = 'e', ['о'] = 'o', ['р'] = 'p', ['с'] = 'c', ['х'] = 'x',
        ['у'] = 'y', ['і'] = 'i', ['ѕ'] = 's', ['ԁ'] = 'd', ['ј'] = 'j', ['ԛ'] = 'q',
        ['ｍ'] = 'm',
        // Greek look-alikes.
        ['ο'] = 'o', ['α'] = 'a', ['ρ'] = 'p', ['ν'] = 'v', ['ι'] = 'i',
        // Digits used in place of letters.
        ['0'] = 'o', ['1'] = 'l', ['3'] = 'e', ['4'] = 'a', ['5'] = 's', ['7'] = 't',
    };

    /// <summary>The skeleton of a host label: lowercase, with confusable characters mapped to ASCII.</summary>
    public static string Skeletonize(string label)
    {
        var builder = new StringBuilder(label.Length);
        foreach (var ch in label.ToLowerInvariant())
        {
            builder.Append(Map.GetValueOrDefault(ch, ch));
        }
        return builder.ToString();
    }
}
