using System.Globalization;
using System.Text;

namespace Obhijatri.Bangla;

/// <summary>Bangla digits (০ to ৯) and number formatting.</summary>
public static class BanglaNumerals
{
    private const char BanglaZero = '০';

    public static char ToBanglaDigit(char c) => c is >= '0' and <= '9' ? (char)(BanglaZero + (c - '0')) : c;

    public static char ToAsciiDigit(char c) => c is >= BanglaZero and <= '৯' ? (char)('0' + (c - BanglaZero)) : c;

    /// <summary>Replaces every ASCII digit in <paramref name="text"/> with its Bangla digit. Other characters are kept.</summary>
    public static string ToBanglaDigits(string text) => Map(text, ToBanglaDigit);

    /// <summary>Replaces every Bangla digit with its ASCII digit, for parsing user input.</summary>
    public static string ToAsciiDigits(string text) => Map(text, ToAsciiDigit);

    public static string ToBanglaDigits(long value) => ToBanglaDigits(value.ToString(CultureInfo.InvariantCulture));

    /// <summary>
    /// Formats a number with a fixed number of decimals (rounded half away from zero), digit grouping,
    /// and optionally Bangla digits. The decimal separator is ".".
    /// </summary>
    public static string Format(decimal value, int decimals = 0, NumberGrouping grouping = NumberGrouping.Lakh, bool banglaDigits = true)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(decimals);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(decimals, 10);

        var rounded = Math.Round(value, decimals, MidpointRounding.AwayFromZero);
        var negative = rounded < 0;
        var text = Math.Abs(rounded).ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

        var point = text.IndexOf('.', StringComparison.Ordinal);
        var integerPart = point < 0 ? text : text[..point];
        var fraction = point < 0 ? string.Empty : text[point..];

        var result = (negative ? "-" : string.Empty) + Group(integerPart, grouping) + fraction;
        return banglaDigits ? ToBanglaDigits(result) : result;
    }

    public static string Format(long value, NumberGrouping grouping = NumberGrouping.Lakh, bool banglaDigits = true) =>
        Format((decimal)value, 0, grouping, banglaDigits);

    private static string Group(string digits, NumberGrouping grouping)
    {
        if (grouping == NumberGrouping.None || digits.Length <= 3)
        {
            return digits;
        }

        var builder = new StringBuilder(digits.Length + digits.Length / 2);
        // The last three digits are always one group; lakh grouping then uses groups of two.
        var head = digits[..^3];
        var size = grouping == NumberGrouping.Lakh ? 2 : 3;
        var first = head.Length % size;
        if (first > 0)
        {
            builder.Append(head, 0, first).Append(',');
        }
        for (var i = first; i < head.Length; i += size)
        {
            builder.Append(head, i, size).Append(',');
        }
        return builder.Append(digits[^3..]).ToString();
    }

    private static string Map(string text, Func<char, char> map)
    {
        var chars = text.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = map(chars[i]);
        }
        return new string(chars);
    }
}
