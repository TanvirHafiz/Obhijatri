using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Obhijatri.Core.Reader;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.SpeechSynthesis;

namespace Obhijatri.App.Services;

/// <summary>
/// Reads text aloud with a Windows Bangla voice, one short piece at a time. Speech is made on this
/// computer by Windows; nothing is sent anywhere.
/// </summary>
internal sealed class SpeechReader : IDisposable
{
    private readonly DispatcherQueue _queue;
    private readonly SpeechSynthesizer _synthesizer = new();
    private readonly MediaPlayer _player = new() { AutoPlay = false };
    private IReadOnlyList<(int Block, string Text)> _chunks = [];
    private int _next;
    private int _generation;

    public SpeechReader(DispatcherQueue queue)
    {
        _queue = queue;
        _player.MediaEnded += (_, _) => _queue.TryEnqueue(() => _ = PlayNextAsync(_generation));
        _player.MediaFailed += (_, _) => _queue.TryEnqueue(() =>
        {
            Stop();
            Failed?.Invoke();
        });
    }

    /// <summary>The block being spoken has changed.</summary>
    public event Action<int>? BlockStarted;

    /// <summary>Everything has been read.</summary>
    public event Action? Finished;

    public event Action? Failed;

    public bool IsPlaying { get; private set; }

    public bool IsPaused { get; private set; }

    /// <summary>A Bangla voice installed in Windows, preferring Bangladesh over India. Null if there is none.</summary>
    public static VoiceInformation? FindBanglaVoice()
    {
        try
        {
            var voices = SpeechSynthesizer.AllVoices.Where(v => v.Language.StartsWith("bn", StringComparison.OrdinalIgnoreCase)).ToList();
            return voices.FirstOrDefault(v => v.Language.Equals("bn-BD", StringComparison.OrdinalIgnoreCase)) ?? voices.FirstOrDefault();
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>Starts reading at <paramref name="firstBlock"/>. Returns false if no Bangla voice is available.</summary>
    public bool Start(IReadOnlyList<string> blocks, int firstBlock)
    {
        if (FindBanglaVoice() is not { } voice)
        {
            return false;
        }

        _synthesizer.Voice = voice;
        var chunks = new List<(int, string)>();
        for (var i = Math.Max(0, firstBlock); i < blocks.Count; i++)
        {
            chunks.AddRange(SpeechChunker.Split(blocks[i]).Select(text => (i, text)));
        }

        Stop();
        _chunks = chunks;
        _next = 0;
        IsPlaying = true;
        IsPaused = false;
        _ = PlayNextAsync(_generation);
        return true;
    }

    public void Pause()
    {
        if (IsPlaying && !IsPaused)
        {
            _player.Pause();
            IsPaused = true;
        }
    }

    public void Resume()
    {
        if (IsPlaying && IsPaused)
        {
            _player.Play();
            IsPaused = false;
        }
    }

    public void Stop()
    {
        _generation++;
        IsPlaying = false;
        IsPaused = false;
        try
        {
            _player.Pause();
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException)
        {
            // The player is already shut down.
        }
    }

    private async Task PlayNextAsync(int generation)
    {
        if (generation != _generation || !IsPlaying)
        {
            return;
        }
        if (_next >= _chunks.Count)
        {
            IsPlaying = false;
            Finished?.Invoke();
            return;
        }

        var (block, text) = _chunks[_next++];
        try
        {
            var stream = await _synthesizer.SynthesizeTextToStreamAsync(text);
            if (generation != _generation)
            {
                stream.Dispose();
                return;
            }
            BlockStarted?.Invoke(block);
            _player.Source = MediaSource.CreateFromStream(stream, stream.ContentType);
            _player.Play();
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or ArgumentException)
        {
            Stop();
            Failed?.Invoke();
        }
    }

    public void Dispose()
    {
        Stop();
        _player.Dispose();
        _synthesizer.Dispose();
    }
}
