using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Obhijatri.Bangla.Bijoy;

namespace Obhijatri.Tests;

public sealed class BijoyConverterTests
{
    private static readonly BijoyConverter Converter = BijoyConverter.Instance;

    /// <summary>
    /// The converter, like the original, writes য়, ড় and ঢ় as single characters (U+09DF, U+09DC,
    /// U+09DD); the same letters can also be typed as two characters. They look identical, so
    /// hand-written expectations are compared in normalised form.
    /// </summary>
    private static string Nfc(string text) => text.Normalize(NormalizationForm.FormC);

    private static IReadOnlyList<(string Bijoy, string Expected)> Fixture()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "bijoy-cases.json")));
        return json.RootElement.GetProperty("cases").EnumerateArray()
            .Select(c => (c.GetProperty("bijoy").GetString()!, c.GetProperty("expected").GetString()!))
            .ToList();
    }

    // ---- Known Bijoy text ----

    [Theory]
    [InlineData("Avwg evsjvq Mvb MvB", "আমি বাংলায় গান গাই")]
    [InlineData("Avgvi †mvbvi evsjv", "আমার সোনার বাংলা")]
    [InlineData("evsjv‡`k", "বাংলাদেশ")]
    [InlineData("cÖ_g Av‡jv", "প্রথম আলো")]
    [InlineData("XvKv", "ঢাকা")]
    [InlineData("weKvk", "বিকাশ")]
    [InlineData("wk¶v", "শিক্ষা")]
    [InlineData("weÁvb", "বিজ্ঞান")]
    [InlineData("¯‹zj", "স্কুল")]
    [InlineData("¯^vaxbZv", "স্বাধীনতা")]
    [InlineData("gyw³hy×", "মুক্তিযুদ্ধ")]
    [InlineData("০১২৩৪৫৬৭৮৯ 0123456789", "০১২৩৪৫৬৭৮৯ ০১২৩৪৫৬৭৮৯")]
    public void KnownBijoy_ConvertsToUnicode(string bijoy, string expected) => Assert.Equal(Nfc(expected), Nfc(Converter.ToUnicode(bijoy)));

    [Fact]
    public void PackageSample_ConvertsAsDocumented()
    {
        const string bijoy = "Dfq cv‡k av‡bi kx‡l †ewóZ cvwb‡Z fvmgvb RvZxq dzj kvcjv| Zvi gv_vq cvUMv‡Qi ci¯úi mshy³ wZbwU cvZv Ges Dfh cv‡k `ywU K‡i ZviKv|";
        Assert.Equal(
            Nfc("উভয় পাশে ধানের শীষে বেষ্টিত পানিতে ভাসমান জাতীয় ফুল শাপলা। তার মাথায় পাটগাছের পরস্পর সংযুক্ত তিনটি পাতা এবং উভয পাশে দুটি করে তারকা।"),
            Nfc(Converter.ToUnicode(bijoy)));
    }

    // ---- Reph (র্): Bijoy puts it after its syllable, Unicode before. The original converter got this wrong. ----

    [Theory]
    [InlineData("Kg©", "কর্ম")]
    [InlineData("gvP©", "মার্চ")]
    [InlineData("me©", "সর্ব")]
    [InlineData("ag©", "ধর্ম")]
    [InlineData("`yMv©", "দুর্গা")]
    [InlineData("A_©", "অর্থ")]
    [InlineData("m~h©", "সূর্য")]
    [InlineData("KvwZ©K", "কার্তিক")]
    [InlineData("Kgx©", "কর্মী")]
    [InlineData("e¨_©", "ব্যর্থ")]
    [InlineData("cvm‡cvU©", "পাসপোর্ট")]
    public void Reph_IsMovedBeforeItsSyllable(string bijoy, string expected) => Assert.Equal(Nfc(expected), Nfc(Converter.ToUnicode(bijoy)));

    [Fact]
    public void RaWithYaPhala_KeepsTheJoiner_SoItIsNotARephBeforeYa()
    {
        // র্যাব as র + ZWJ + ্য + া + ব, the way it is written and rendered.
        Assert.Equal("র‍্যাব", Converter.ToUnicode("i¨ve"));
    }

    // ---- Differential test against the original package ----

    [Fact]
    public void MatchesTheOriginalPackage_ForEverythingWithoutAReph()
    {
        var cases = Fixture();
        Assert.True(cases.Count > 900);

        var checkedCount = 0;
        foreach (var (bijoy, expected) in cases)
        {
            // The original mishandles reph, which this port fixes on purpose (tests above).
            if (expected.Contains("র্", StringComparison.Ordinal))
            {
                continue;
            }
            checkedCount++;
            Assert.True(expected == Converter.ToUnicode(bijoy), $"{bijoy} -> {Converter.ToUnicode(bijoy)} (original gives {expected})");
        }

        Assert.True(checkedCount > 850, $"only {checkedCount} cases compared");
    }

    // ---- Robustness ----

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void EmptyInput_GivesEmpty(string? input) => Assert.Equal(string.Empty, Converter.ToUnicode(input));

    [Fact]
    public void RandomInput_NeverThrows_AndIsDeterministic()
    {
        var random = new Random(12345);
        // Characters that matter to Bijoy: ASCII, the Latin-1 and Windows-1252 range, Bangla, joiners.
        var alphabet = Enumerable.Range(0x20, 0x5F).Concat(Enumerable.Range(0xA0, 0x60)).Concat(Enumerable.Range(0x2018, 8))
            .Concat(Enumerable.Range(0x0980, 0x80)).Concat([0x2020, 0x2021, 0x2022, 0x2026, 0x200C, 0x200D, 0x0A, 0x09])
            .Select(c => (char)c).ToArray();

        for (var round = 0; round < 20_000; round++)
        {
            var length = random.Next(0, 40);
            var chars = new char[length];
            for (var i = 0; i < length; i++)
            {
                chars[i] = alphabet[random.Next(alphabet.Length)];
            }
            var text = new string(chars);

            var first = Converter.ToUnicode(text);
            Assert.Equal(first, Converter.ToUnicode(text));
        }
    }

    [Fact]
    public void LargeText_ConvertsQuickly()
    {
        var page = string.Join(' ', Enumerable.Repeat("Avwg evsjvq Mvb MvB, cÖ_g Av‡jv †`‡L wk¶v|", 2500)); // about 100 KB
        var watch = Stopwatch.StartNew();
        var result = Converter.ToUnicode(page);
        watch.Stop();

        Assert.StartsWith(Nfc("আমি বাংলায় গান গাই, প্রথম আলো দেখে শিক্ষা।"), Nfc(result), StringComparison.Ordinal);
        Assert.True(watch.ElapsedMilliseconds < 1500, $"took {watch.ElapsedMilliseconds} ms");
    }
}
