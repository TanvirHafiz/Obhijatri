using Obhijatri.Bangla;

namespace Obhijatri.Tests;

public sealed class BanglaNumeralsTests
{
    [Theory]
    [InlineData('0', '০')]
    [InlineData('1', '১')]
    [InlineData('2', '২')]
    [InlineData('3', '৩')]
    [InlineData('4', '৪')]
    [InlineData('5', '৫')]
    [InlineData('6', '৬')]
    [InlineData('7', '৭')]
    [InlineData('8', '৮')]
    [InlineData('9', '৯')]
    public void EachDigit_MapsBothWays(char ascii, char bangla)
    {
        Assert.Equal(bangla, BanglaNumerals.ToBanglaDigit(ascii));
        Assert.Equal(ascii, BanglaNumerals.ToAsciiDigit(bangla));
    }

    [Theory]
    [InlineData("2026", "২০২৬")]
    [InlineData("25 Sep 2026, 17:18", "২৫ Sep ২০২৬, ১৭:১৮")]
    [InlineData("Ctrl+T", "Ctrl+T")]
    [InlineData("", "")]
    [InlineData("শেষ 24 ঘণ্টা", "শেষ ২৪ ঘণ্টা")]
    public void Text_OnlyDigitsChange(string input, string expected)
    {
        Assert.Equal(expected, BanglaNumerals.ToBanglaDigits(input));
        Assert.Equal(input, BanglaNumerals.ToAsciiDigits(expected));
    }

    [Theory]
    [InlineData(0, "০")]
    [InlineData(7, "৭")]
    [InlineData(999, "৯৯৯")]
    [InlineData(1000, "১,০০০")]
    [InlineData(12345, "১২,৩৪৫")]
    [InlineData(100000, "১,০০,০০০")]
    [InlineData(1234567, "১২,৩৪,৫৬৭")]
    [InlineData(10000000, "১,০০,০০,০০০")]
    [InlineData(-1234567, "-১২,৩৪,৫৬৭")]
    public void Integers_UseLakhGrouping(long value, string expected)
    {
        Assert.Equal(expected, BanglaNumerals.Format(value));
    }

    [Theory]
    [InlineData(1000, "১,০০০")]
    [InlineData(1234567, "১,২৩৪,৫৬৭")]
    [InlineData(1234567890, "১,২৩৪,৫৬৭,৮৯০")]
    public void Integers_ThousandsGrouping(long value, string expected)
    {
        Assert.Equal(expected, BanglaNumerals.Format(value, NumberGrouping.Thousands));
    }

    [Fact]
    public void NoGrouping_AndAsciiDigits()
    {
        Assert.Equal("১২৩৪৫৬৭", BanglaNumerals.Format(1234567, NumberGrouping.None));
        Assert.Equal("12,34,567", BanglaNumerals.Format(1234567, NumberGrouping.Lakh, banglaDigits: false));
    }

    [Theory]
    [InlineData("2.5", 1, "২.৫")]
    [InlineData("2.5", 2, "২.৫০")]
    [InlineData("0.05", 1, "০.১")]
    [InlineData("1234.5", 2, "১,২৩৪.৫০")]
    [InlineData("1234567.891", 2, "১২,৩৪,৫৬৭.৮৯")]
    [InlineData("-0.25", 1, "-০.৩")]
    [InlineData("0.5", 0, "১")]
    [InlineData("99.999", 2, "১০০.০০")]
    public void Decimals_RoundHalfAwayFromZero(string value, int decimals, string expected)
    {
        var number = decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(expected, BanglaNumerals.Format(number, decimals));
    }

    [Fact]
    public void NegativeZeroAfterRounding_HasNoMinus()
    {
        Assert.Equal("০.০", BanglaNumerals.Format(-0.01m, 1));
    }

    [Fact]
    public void InvalidDecimals_Throw()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BanglaNumerals.Format(1m, -1));
    }
}
