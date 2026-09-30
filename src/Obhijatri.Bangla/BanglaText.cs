namespace Obhijatri.Bangla;

/// <summary>Small checks on whether text is Bangla.</summary>
public static class BanglaText
{
    /// <summary>The Bangla letters, signs and digits block.</summary>
    public static bool IsBanglaChar(char c) => c is >= 'ঀ' and <= '৿';

    /// <summary>
    /// True when at least <paramref name="fraction"/> of the letters in the text are Bangla, so a
    /// paragraph that is already Bangla is not sent to be translated. Text with no letters counts as Bangla
    /// (there is nothing to translate).
    /// </summary>
    public static bool IsMostlyBangla(string text, double fraction = 0.5)
    {
        var letters = 0;
        var bangla = 0;
        foreach (var c in text)
        {
            if (!char.IsLetter(c) && !IsBanglaChar(c))
            {
                continue;
            }
            letters++;
            if (IsBanglaChar(c))
            {
                bangla++;
            }
        }
        return letters == 0 || bangla >= letters * fraction;
    }
}
