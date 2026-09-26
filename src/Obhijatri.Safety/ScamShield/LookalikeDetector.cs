using System.Globalization;

namespace Obhijatri.Safety.ScamShield;

/// <summary>
/// Flags a host that looks like one of <see cref="ProtectedBrands.All"/> but is not really that
/// site: a punycode or homoglyph look-alike (a Cyrillic "о" standing in for "o"), a one or
/// two-character typo (bkaash, faceboook), or the brand's name used as a label somewhere in an
/// unrelated domain (bkash-verify.xyz, bkash.secure-login.info). A real subdomain of the protected
/// domain itself (pay.bkash.com, accounts.google.com) is never flagged, because its registrable
/// domain is the protected domain.
/// </summary>
public static class LookalikeDetector
{
    private static readonly IdnMapping Idn = new();

    public static ScamVerdict? Check(Uri uri) => Check(uri.Host);

    public static ScamVerdict? Check(string host)
    {
        var asciiHost = host.Trim().TrimEnd('.').ToLowerInvariant();
        if (asciiHost.Length == 0)
        {
            return null;
        }

        var registrableAscii = RegistrableDomain.Get(asciiHost);
        if (ProtectedBrands.All.Any(b => string.Equals(b.Domain, registrableAscii, StringComparison.Ordinal)))
        {
            return null; // the real site, or a real subdomain of it
        }

        string unicodeHost;
        try
        {
            unicodeHost = asciiHost.Contains("xn--", StringComparison.Ordinal) ? Idn.GetUnicode(asciiHost) : asciiHost;
        }
        catch (ArgumentException)
        {
            unicodeHost = asciiHost; // not valid punycode; fall through to the plain checks below
        }

        var registrableUnicode = RegistrableDomain.Get(unicodeHost);
        var primaryLabel = registrableUnicode[..(registrableUnicode.IndexOf('.') is >= 0 and var dot ? dot : registrableUnicode.Length)];
        var skeleton = Homoglyphs.Skeletonize(primaryLabel);
        var labels = SplitLabels(asciiHost);

        foreach (var brand in ProtectedBrands.All)
        {
            var token = ProtectedBrands.Token(brand);

            // Same word once look-alike characters are normalised, but a different domain: a
            // punycode or homoglyph attack, or a plain digit-for-letter substitution.
            if (skeleton == token)
            {
                return ScamVerdict.Lookalike(brand);
            }

            // The brand's name appears as a whole label (or hyphen-joined part of one) somewhere
            // in the host, but the site itself is not the brand's domain.
            if (token.Length >= 4 && labels.Contains(token))
            {
                return ScamVerdict.Lookalike(brand);
            }

            // A one-character typo of the brand's name (an extra, missing or swapped letter). Only
            // for tokens of 5 letters or more: with anything shorter, a distance-1 match is common
            // between two unrelated short words (for example "brac" and "brta").
            if (token.Length >= 5 && Math.Abs(skeleton.Length - token.Length) <= 1 && Levenshtein(skeleton, token) == 1)
            {
                return ScamVerdict.Lookalike(brand);
            }
        }

        return null;
    }

    /// <summary>Every dot and hyphen separated piece of the host, lowercase.</summary>
    private static HashSet<string> SplitLabels(string host)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var dotPart in host.Split('.'))
        {
            foreach (var part in dotPart.Split('-'))
            {
                if (part.Length > 0)
                {
                    result.Add(part);
                }
            }
        }
        return result;
    }

    /// <summary>Plain edit distance, capped early once it is obviously past 2 (this only needs small distances).</summary>
    private static int Levenshtein(string a, string b)
    {
        var rows = a.Length + 1;
        var cols = b.Length + 1;
        var previous = new int[cols];
        var current = new int[cols];
        for (var j = 0; j < cols; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i < rows; i++)
        {
            current[0] = i;
            for (var j = 1; j < cols; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }
            (previous, current) = (current, previous);
        }
        return previous[^1];
    }
}
