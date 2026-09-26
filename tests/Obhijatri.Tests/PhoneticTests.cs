using System.Text.Json;
using Obhijatri.Bangla.Phonetic;

namespace Obhijatri.Tests;

public sealed class PhoneticTests
{
    public static TheoryData<string, string> ReferenceCases()
    {
        // Expected values were produced by running the original Avro library (see the fixture's "source").
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "phonetic-cases.json")));
        var data = new TheoryData<string, string>();
        foreach (var c in json.RootElement.GetProperty("cases").EnumerateArray())
        {
            data.Add(c.GetProperty("input").GetString()!, c.GetProperty("expected").GetString()!);
        }
        return data;
    }

    [Fact]
    public void PlanSentence_Converts()
    {
        // Compare canonical forms: য় may be one code point or য plus nukta, which look identical.
        var expected = "আমি বাংলায় গান গাই".Normalize(System.Text.NormalizationForm.FormC);
        Assert.Equal(expected, AvroPhonetic.Instance.Convert("ami banglay gan gai").Normalize(System.Text.NormalizationForm.FormC));
    }

    [Fact]
    public void Fixture_HasAtLeastFiftyCases()
    {
        Assert.True(ReferenceCases().Count >= 50);
    }

    [Theory]
    [MemberData(nameof(ReferenceCases))]
    public void MatchesOriginalAvro(string input, string expected)
    {
        Assert.Equal(expected, AvroPhonetic.Instance.Convert(input));
    }

    [Theory]
    [InlineData("tomar", "তোমার")]
    [InlineData("dhaka", "ঢাকা")]
    [InlineData("shokal", "সকাল")]
    [InlineData("sotti", "সত্যি")]
    [InlineData("bhalobasha", "ভালোবাসা")]
    [InlineData("bishwobidyaloy", "বিশ্ববিদ্যালয়")]
    [InlineData("shikkha", "শিক্ষা")]
    [InlineData("poysa", "পয়সা")]
    [InlineData("meye", "মেয়ে")]
    [InlineData("bangladesh", "বাংলাদেশ")]
    [InlineData("dhonnobad", "ধন্যবাদ")]
    [InlineData("bikash", "বিকাশ")]
    public void Suggestions_OfferTheCommonSpelling(string typed, string word)
    {
        Assert.Contains(word, PhoneticSuggester.Instance.Suggest(typed));
    }

    [Fact]
    public void Suggestions_StartWithTheExactAvroResult_AndHaveNoDuplicates()
    {
        var list = PhoneticSuggester.Instance.Suggest("bangla");
        Assert.Equal("বাংলা", list[0]);
        Assert.Equal(list.Count, list.Distinct().Count());
        Assert.True(list.Count <= PhoneticSuggester.DefaultMax);
    }

    [Fact]
    public void Suggestions_ForEmptyInput_AreEmpty()
    {
        Assert.Empty(PhoneticSuggester.Instance.Suggest("   "));
    }

    [Theory]
    [InlineData("tomar", "তোমার")]
    [InlineData("tOmar", "তোমার")]
    [InlineData("Dhaka", "ঢাকা")]
    [InlineData("sotti", "সত্যি")]
    public void LooseKey_MatchesAcrossScripts(string roman, string bangla)
    {
        Assert.Equal(LooseKey.FromBangla(bangla), LooseKey.FromRoman(roman));
    }

    [Fact]
    public void WordList_IsLoaded()
    {
        Assert.True(PhoneticSuggester.Instance.WordCount > 800);
    }

    [Fact]
    public void IsConvertible_CoversLettersDigitsAndRulePunctuation()
    {
        Assert.True(AvroPhonetic.Instance.IsConvertible('a'));
        Assert.True(AvroPhonetic.Instance.IsConvertible('5'));
        Assert.True(AvroPhonetic.Instance.IsConvertible('.'));
        Assert.True(AvroPhonetic.Instance.IsConvertible('$'));
        Assert.False(AvroPhonetic.Instance.IsConvertible(' '));
    }
}
