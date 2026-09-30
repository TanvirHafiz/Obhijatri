using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Obhijatri.App.Localization;
using Obhijatri.App.Services;
using Obhijatri.Core.Reader;

namespace Obhijatri.App.Views;

/// <summary>
/// Reader mode: the article's text in a clean, large, single column, with the option to have it read
/// aloud by a Windows Bangla voice (or a note on how to install one). Text only: no images, styles or
/// scripts from the page ever reach this view, and every block is drawn as plain text.
/// </summary>
public sealed partial class ReaderView : UserControl, IDisposable
{
    private const int HeadingExtraSize = 4;
    private const int TitleExtraSize = 10;

    private readonly ReaderArticle _article;
    private readonly Action _close;
    private readonly List<Border> _blockViews = [];
    private readonly List<TextBlock> _blockTexts = [];
    private readonly List<int> _extraSizes = [];
    private readonly List<string> _speechBlocks = [];
    private readonly SpeechReader? _speech;
    private readonly bool _hasVoice;
    private int _current = -1;

    /// <param name="translate">Translates one piece of text to Bangla (the local AI), or null when that is not switched on.</param>
    internal ReaderView(ReaderArticle article, Action close, Func<string, CancellationToken, Task<string?>>? translate = null)
    {
        _article = article;
        _close = close;
        _translate = translate;
        InitializeComponent();

        PlayText.Text = Strings.Get("ReaderPlay");
        StopText.Text = Strings.Get("ReaderStop");
        StatusText.Text = article.Site.Length > 0 ? article.Site : Strings.Get("ReaderTapHint");
        ToolTipService.SetToolTip(SmallerButton, Strings.Get("ReaderSmaller"));
        ToolTipService.SetToolTip(LargerButton, Strings.Get("ReaderLarger"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(SmallerButton, Strings.Get("ReaderSmaller"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(LargerButton, Strings.Get("ReaderLarger"));
        ToolTipService.SetToolTip(CloseButton, Strings.Get("ReaderClose"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(CloseButton, Strings.Get("ReaderClose"));
        SpeechSettingsButton.Content = Strings.Get("ReaderOpenSpeechSettings");
        NoVoiceBar.Title = Strings.Get("ReaderNoVoiceTitle");
        NoVoiceBar.Message = Strings.Get("ReaderNoVoiceMessage");

        _hasVoice = SpeechReader.FindBanglaVoice() is not null;
        if (_hasVoice)
        {
            _speech = new SpeechReader(Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
            _speech.BlockStarted += Speech_BlockStarted;
            _speech.Finished += Speech_Finished;
            _speech.Failed += Speech_Failed;
        }
        else
        {
            PlayButton.IsEnabled = false;
            StopButton.IsEnabled = false;
            NoVoiceBar.IsOpen = true;
        }

        Build();
        InitializeTranslation();
        Unloaded += (_, _) => Dispose();
    }

    private void Build()
    {
        var size = AppServices.Settings.ReaderFontSize;

        AddBlock(_article.Title.Length > 0 ? _article.Title : _article.Site, "TitleTextBlockStyle", TitleExtraSize, size, speak: true);
        foreach (var block in _article.Blocks)
        {
            // The page title usually shows again as the first heading.
            if (block.Kind == ReaderBlockKind.Heading && _blockViews.Count == 1 && block.Text == _article.Title)
            {
                continue;
            }

            var text = block.Kind == ReaderBlockKind.ListItem ? "• " + block.Text : block.Text; // not-ui
            AddBlock(text, block.Kind == ReaderBlockKind.Heading ? "SubtitleTextBlockStyle" : null,
                block.Kind == ReaderBlockKind.Heading ? HeadingExtraSize : 0, size, speak: true, speechText: block.Text,
                italic: block.Kind == ReaderBlockKind.Quote);
        }
    }

    private void AddBlock(string text, string? styleKey, int extraSize, int size, bool speak, string? speechText = null, bool italic = false)
    {
        var view = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true,
            FontSize = size + extraSize,
            LineHeight = (size + extraSize) * 1.65,
            FontStyle = italic ? Windows.UI.Text.FontStyle.Italic : Windows.UI.Text.FontStyle.Normal,
        };
        if (styleKey is not null)
        {
            view.Style = (Style)Application.Current.Resources[styleKey];
            view.FontSize = size + extraSize;
        }

        var index = _blockViews.Count;
        var border = new Border { Child = view, Style = (Style)Application.Current.Resources["ReaderBlockStyle"] };
        border.Tapped += (_, _) => StartFrom(index);
        _blockViews.Add(border);
        _blockTexts.Add(view);
        _extraSizes.Add(extraSize);
        _speechBlocks.Add(speak ? speechText ?? text : string.Empty);
        ArticlePanel.Children.Add(border);
    }

    // ---- Reading aloud ----

    private void StartFrom(int index)
    {
        if (_speech is null)
        {
            return;
        }

        if (_speech.Start(_speechBlocks, index))
        {
            PlayText.Text = Strings.Get("ReaderPause");
            PlayIcon.Glyph = "";
        }
    }

    private void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        if (_speech is null)
        {
            return;
        }

        if (!_speech.IsPlaying)
        {
            StartFrom(0);
        }
        else if (_speech.IsPaused)
        {
            _speech.Resume();
            PlayText.Text = Strings.Get("ReaderPause");
            PlayIcon.Glyph = "";
        }
        else
        {
            _speech.Pause();
            PlayText.Text = Strings.Get("ReaderPlay");
            PlayIcon.Glyph = "";
        }
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        _speech?.Stop();
        ResetPlayButton();
        Highlight(-1);
    }

    private void Speech_BlockStarted(int block)
    {
        Highlight(block);
        StatusText.Text = SiteOrHint();
    }

    private void Speech_Finished()
    {
        ResetPlayButton();
        Highlight(-1);
    }

    private void Speech_Failed()
    {
        ResetPlayButton();
        Highlight(-1);
        StatusText.Text = Strings.Get("ReaderSpeechFailed");
    }

    private string SiteOrHint() => _article.Site.Length > 0 ? _article.Site : Strings.Get("ReaderTapHint");

    private void ResetPlayButton()
    {
        PlayText.Text = Strings.Get("ReaderPlay");
        PlayIcon.Glyph = "";
    }

    /// <summary>Marks the block being read and keeps it in view.</summary>
    private void Highlight(int block)
    {
        if (_current >= 0 && _current < _blockViews.Count)
        {
            _blockViews[_current].Style = (Style)Application.Current.Resources["ReaderBlockStyle"];
        }
        _current = block;
        if (block >= 0 && block < _blockViews.Count)
        {
            _blockViews[block].Style = (Style)Application.Current.Resources["ReaderCurrentBlockStyle"];
            _blockViews[block].StartBringIntoView(new BringIntoViewOptions { AnimationDesired = true, VerticalAlignmentRatio = 0.3 });
        }
    }

    private async void SpeechSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        // Opens the Windows speech settings page, where voices are added.
        await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings:speech")); // not-ui
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => _close();

    // ---- Text size ----

    private void SmallerButton_Click(object sender, RoutedEventArgs e) => ChangeSize(-2);

    private void LargerButton_Click(object sender, RoutedEventArgs e) => ChangeSize(+2);

    private void ChangeSize(int step)
    {
        AppServices.Settings.ReaderFontSize += step;
        var size = AppServices.Settings.ReaderFontSize;
        for (var i = 0; i < _blockTexts.Count; i++)
        {
            _blockTexts[i].FontSize = size + _extraSizes[i];
            _blockTexts[i].LineHeight = (size + _extraSizes[i]) * 1.65;
        }
    }

    public void Dispose()
    {
        _translation?.Cancel();
        _translation?.Dispose();
        _speech?.Dispose();
    }
}
