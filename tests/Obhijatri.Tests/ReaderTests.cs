using System.Text.Json;
using Obhijatri.Core.Reader;
using Obhijatri.Core.Settings;
using Obhijatri.Core.Storage;

namespace Obhijatri.Tests;

public sealed class ReaderTests
{
    private static string Json(string title, params (string Kind, string Text)[] blocks) =>
        JsonSerializer.Serialize(new
        {
            title,
            site = "prothomalo.com",
            blocks = blocks.Select(b => new { t = b.Kind, x = b.Text }),
        });

    // ---- ReaderArticle.Parse ----

    [Fact]
    public void Parse_GoodArticle_KeepsBlocksInOrder()
    {
        var article = ReaderArticle.Parse(Json("প্রথম আলো", ("h", "শিরোনাম"), ("p", "প্রথম অনুচ্ছেদ।"), ("li", "একটি তালিকা"), ("q", "উদ্ধৃতি"), ("p", "দ্বিতীয় অনুচ্ছেদ।")));

        Assert.NotNull(article);
        Assert.Equal("প্রথম আলো", article.Title);
        Assert.Equal("prothomalo.com", article.Site);
        Assert.Equal(
            [ReaderBlockKind.Heading, ReaderBlockKind.Paragraph, ReaderBlockKind.ListItem, ReaderBlockKind.Quote, ReaderBlockKind.Paragraph],
            article.Blocks.Select(b => b.Kind));
    }

    [Fact]
    public void Parse_TooFewParagraphs_IsNotAnArticle()
    {
        Assert.Null(ReaderArticle.Parse(Json("t", ("h", "শিরোনাম"), ("p", "একটি মাত্র অনুচ্ছেদ"))));
        Assert.Null(ReaderArticle.Parse(Json("t")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("42")]
    [InlineData("{\"blocks\": 5}")]
    [InlineData("{\"title\": \"x\"}")]
    public void Parse_NotTheExpectedShape_ReturnsNull(string? json) => Assert.Null(ReaderArticle.Parse(json));

    [Fact]
    public void Parse_DropsUnknownKindsAndBadItems_ButKeepsTheRest()
    {
        var json = "{\"title\":\"t\",\"blocks\":[{\"t\":\"script\",\"x\":\"alert(1)\"},{\"t\":\"p\",\"x\":5},{\"t\":\"p\"},7,"
                   + "{\"t\":\"p\",\"x\":\"এক\"},{\"t\":\"p\",\"x\":\"দুই\"}]}";

        var article = ReaderArticle.Parse(json);

        Assert.NotNull(article);
        Assert.Equal(["এক", "দুই"], article.Blocks.Select(b => b.Text));
    }

    [Fact]
    public void Parse_RemovesControlAndBidiCharacters_CollapsesSpaces_KeepsBanglaJoiners()
    {
        // U+202E reverses text direction; U+200D is needed inside Bangla conjuncts (য়, র‍্য).
        var messy = "a\u0000b‮c⁦d  \t\n e​f র‍্যx";
        var article = ReaderArticle.Parse(Json("t", ("p", messy), ("p", "দ্বিতীয়")));

        Assert.NotNull(article);
        Assert.Equal("abcd ef র‍্যx", article.Blocks[0].Text);
    }

    [Fact]
    public void Parse_CapsLengthsAndCounts()
    {
        var huge = new string('ক', ReaderArticle.MaxBlockLength + 500);
        var article = ReaderArticle.Parse(Json(new string('শ', 1000), ("p", huge), ("p", "দুই")));
        Assert.NotNull(article);
        Assert.Equal(ReaderArticle.MaxBlockLength, article.Blocks[0].Text.Length);
        Assert.Equal(ReaderArticle.MaxTitleLength, article.Title.Length);

        var many = Enumerable.Range(0, ReaderArticle.MaxBlocks + 300).Select(i => ("p", "অনুচ্ছেদ " + i)).ToArray();
        Assert.Equal(ReaderArticle.MaxBlocks, ReaderArticle.Parse(Json("t", many))!.Blocks.Count);
    }

    [Fact]
    public void Parse_HtmlInTextStaysPlainText()
    {
        // Blocks are drawn as plain text, so markup is just characters; Parse must not alter or run it.
        var article = ReaderArticle.Parse(Json("<b>t</b>", ("p", "<img src=x onerror=alert(1)>"), ("p", "দুই")));
        Assert.NotNull(article);
        Assert.Equal("<img src=x onerror=alert(1)>", article.Blocks[0].Text);
    }

    [Fact]
    public void Parse_DeeplyNestedJson_IsRejected() =>
        Assert.Null(ReaderArticle.Parse(new string('[', 200) + new string(']', 200)));

    // ---- SpeechChunker ----

    [Fact]
    public void Split_ShortText_IsOneChunk() =>
        Assert.Equal(["আমি বাংলায় গান গাই।"], SpeechChunker.Split("আমি বাংলায় গান গাই।"));

    [Fact]
    public void Split_LongText_BreaksAtSentenceEnds_AndRespectsTheLimit()
    {
        var sentence = "এটি একটি পরীক্ষার বাক্য।";
        var text = string.Join(' ', Enumerable.Repeat(sentence, 40));

        var chunks = SpeechChunker.Split(text, 100);

        Assert.True(chunks.Count > 5);
        Assert.All(chunks, c =>
        {
            Assert.True(c.Length <= 100, c);
            Assert.EndsWith("।", c);
        });
        Assert.Equal(text, string.Join(' ', chunks));
    }

    [Fact]
    public void Split_OneVeryLongSentence_IsCutAtSpaces()
    {
        var text = string.Join(' ', Enumerable.Repeat("শব্দ", 200));

        var chunks = SpeechChunker.Split(text, 50);

        Assert.All(chunks, c => Assert.True(c.Length <= 50));
        Assert.Equal(text, string.Join(' ', chunks));
    }

    [Fact]
    public void Split_NoSpacesAtAll_StillTerminatesWithinLimit()
    {
        var chunks = SpeechChunker.Split(new string('ক', 1000), 100);

        Assert.All(chunks, c => Assert.True(c.Length <= 100));
        Assert.Equal(1000, chunks.Sum(c => c.Length));
    }

    [Fact]
    public void Split_HandlesEnglishPunctuation_NotDecimalsOrAbbreviationsMidWord()
    {
        var chunks = SpeechChunker.Split("Price is 3.5 taka. Really? Yes!", 20);
        Assert.Equal("Price is 3.5 taka. Really? Yes!", string.Join(" ", chunks));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Split_EmptyText_GivesNothing(string text) => Assert.Empty(SpeechChunker.Split(text));

    // ---- Settings ----

    [Fact]
    public void Settings_ReaderAndFontDefaultsAndLimits()
    {
        using var db = BrowserDatabase.OpenInMemory();
        var store = new SettingsStore(db);
        var settings = new BrowserSettings(store);

        Assert.False(settings.FixBanglaFonts);
        Assert.Equal(BrowserSettings.DefaultReaderFontSize, settings.ReaderFontSize);

        settings.ReaderFontSize = 100;
        Assert.Equal(BrowserSettings.MaxReaderFontSize, settings.ReaderFontSize);
        settings.ReaderFontSize = 1;
        Assert.Equal(BrowserSettings.MinReaderFontSize, settings.ReaderFontSize);
        settings.ReaderFontSize += 2;
        Assert.Equal(BrowserSettings.MinReaderFontSize + 2, settings.ReaderFontSize);

        store.SetString(BrowserSettings.Keys.ReaderFontSize, "huge");
        Assert.Equal(BrowserSettings.DefaultReaderFontSize, settings.ReaderFontSize);

        settings.FixBanglaFonts = true;
        Assert.True(settings.FixBanglaFonts);
    }
}
