using Microsoft.UI.Xaml;
using Obhijatri.App.Localization;
using Obhijatri.Bangla;
using Obhijatri.Core.Reader;
using Obhijatri.AI.Ollama;

namespace Obhijatri.App.Views;

/// <summary>
/// Translating the article into Bangla with the local AI (Ollama), block by block, so that the text
/// never leaves this computer. Paragraphs that are already Bangla are left alone. The translation can be
/// stopped, and the original text shown again at any time. Every answer is shown as plain text.
/// </summary>
public sealed partial class ReaderView
{
    private const int MaxFailuresBeforeGivingUp = 2;

    private readonly Func<string, CancellationToken, Task<string?>>? _translate;
    private CancellationTokenSource? _translation;
    private List<string> _originalDisplay = [];
    private List<string> _originalSpeech = [];
    private readonly Dictionary<int, string> _translatedSpeech = [];
    private bool _showingTranslation;

    private void InitializeTranslation()
    {
        _originalDisplay = _blockTexts.Select(t => t.Text).ToList();
        _originalSpeech = [.. _speechBlocks];

        if (_translate is null)
        {
            return;
        }

        TranslateButton.Visibility = Visibility.Visible;
        TranslateText.Text = Strings.Get("ReaderTranslate");
    }

    /// <summary>Starts translating from the top (used when the person chose translation from the menu).</summary>
    public void StartTranslation()
    {
        if (_translate is not null && _translation is null)
        {
            _ = RunTranslationAsync();
        }
    }

    private void TranslateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_translation is not null)
        {
            _translation.Cancel(); // stop: what is done so far stays
            return;
        }

        if (_translatedSpeech.Count > 0)
        {
            SetShowingTranslation(!_showingTranslation);
            return;
        }

        _ = RunTranslationAsync();
    }

    private async Task RunTranslationAsync()
    {
        if (_translate is null)
        {
            return;
        }

        _translation = new CancellationTokenSource();
        var token = _translation.Token;
        TranslateText.Text = Strings.Get("ReaderTranslateStop");

        var todo = Enumerable.Range(0, _originalSpeech.Count)
            .Where(i => _originalSpeech[i].Length > 0 && !_translatedSpeech.ContainsKey(i) && !BanglaText.IsMostlyBangla(_originalSpeech[i]))
            .ToList();
        if (todo.Count == 0)
        {
            StatusText.Text = Strings.Get("ReaderTranslateNothing");
            EndTranslation();
            return;
        }

        var done = 0;
        var failures = 0;
        try
        {
            for (var n = 0; n < todo.Count && !token.IsCancellationRequested; n++)
            {
                var index = todo[n];
                StatusText.Text = Strings.Format("ReaderTranslatingFormat", Formatting.Number(n + 1), Formatting.Number(todo.Count));

                var parts = new List<string>();
                var complete = true;
                foreach (var piece in SpeechChunker.Split(_originalSpeech[index], TranslationPrompt.MaxTextLength))
                {
                    var answer = await _translate(piece, token);
                    if (answer is null)
                    {
                        complete = false;
                        break;
                    }
                    parts.Add(answer);
                }

                if (token.IsCancellationRequested)
                {
                    break;
                }
                if (!complete)
                {
                    // Ollama is not answering. Give up early rather than wait for every paragraph.
                    if (++failures >= MaxFailuresBeforeGivingUp && done == 0)
                    {
                        break;
                    }
                    continue;
                }

                _translatedSpeech[index] = string.Join(' ', parts);
                _showingTranslation = true;
                ShowBlock(index, translated: true);
                done++;
            }
        }
        finally
        {
            StatusText.Text = done > 0
                ? Strings.Format("ReaderTranslatedFormat", Formatting.Number(done))
                : token.IsCancellationRequested ? Strings.Get("ReaderTranslateStopped") : Strings.Get("ReaderTranslateFailed");
            EndTranslation();
        }
    }

    private void EndTranslation()
    {
        _translation?.Dispose();
        _translation = null;
        TranslateText.Text = _translatedSpeech.Count > 0
            ? Strings.Get(_showingTranslation ? "ReaderShowOriginal" : "ReaderShowTranslation")
            : Strings.Get("ReaderTranslate");
    }

    private void SetShowingTranslation(bool translated)
    {
        _showingTranslation = translated;
        foreach (var index in _translatedSpeech.Keys)
        {
            ShowBlock(index, translated);
        }
        TranslateText.Text = Strings.Get(translated ? "ReaderShowOriginal" : "ReaderShowTranslation");
    }

    /// <summary>Shows the translation (or the original) of one block, and reads that version aloud from now on.</summary>
    private void ShowBlock(int index, bool translated)
    {
        var speech = translated ? _translatedSpeech[index] : _originalSpeech[index];
        var original = _originalDisplay[index];
        var prefix = original.Length > _originalSpeech[index].Length && original.EndsWith(_originalSpeech[index], StringComparison.Ordinal)
            ? original[..(original.Length - _originalSpeech[index].Length)]
            : string.Empty;

        _blockTexts[index].Text = translated ? prefix + speech : original;
        _speechBlocks[index] = speech;
    }

#if DEBUG
    // Developer self-test access.
    internal bool DebugIsTranslating => _translation is not null;
    internal string DebugStatus => StatusText.Text;
    internal IReadOnlyList<string> DebugBlockTexts => _blockTexts.Select(b => b.Text).ToList();
    internal void DebugToggleTranslation() => TranslateButton_Click(this, new RoutedEventArgs());
#endif
}
