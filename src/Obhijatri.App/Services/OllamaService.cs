using Obhijatri.AI.Ollama;
using Obhijatri.AI.Scoring;

namespace Obhijatri.App.Services;

/// <summary>
/// Layer 2 of "এটা কি প্রতারণা?": asks an Ollama server on this computer for a plain Bangla
/// explanation. The address is fixed to the loopback address Ollama listens on by default. Page text
/// goes nowhere else, and no system proxy is used, so it cannot be routed off this machine.
/// </summary>
internal static class OllamaService
{
    private static readonly HttpClient Http = new(new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        UseProxy = false,
        ConnectTimeout = TimeSpan.FromSeconds(3),
    });

    private static Uri Endpoint
    {
        get
        {
#if DEBUG
            // Developer self-tests point this at a fake server; the client still accepts loopback addresses only.
            if (Environment.GetEnvironmentVariable("OBHIJATRI_OLLAMA_URL") is { Length: > 0 } custom && Uri.TryCreate(custom, UriKind.Absolute, out var uri))
            {
                return uri;
            }
#endif
            return OllamaClient.DefaultEndpoint;
        }
    }

    /// <summary>A translation of one piece of text can take a while on a small computer.</summary>
    public static readonly TimeSpan TranslationTimeout = TimeSpan.FromSeconds(90);

    private static OllamaClient? CreateClient(TimeSpan? timeout = null)
    {
        try
        {
            return new OllamaClient(Http, Endpoint, AppServices.Settings.OllamaModel, timeout);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>The installed model names, or null if Ollama does not answer within two seconds.</summary>
    public static Task<IReadOnlyList<string>?> ListModelsAsync() =>
        CreateClient() is { } client ? client.ListModelsAsync(TimeSpan.FromSeconds(2)) : Task.FromResult<IReadOnlyList<string>?>(null);

    /// <summary>A short Bangla explanation, or null (not enabled, not running, model missing, too slow, cancelled).</summary>
    public static async Task<string?> ExplainAsync(ScamAssessment assessment, PageSignals page, CancellationToken cancellation)
    {
        if (!AppServices.Settings.OllamaEnabled || CreateClient() is not { } client)
        {
            return null;
        }
        return await client.ExplainAsync(ExplanationPrompt.Build(assessment, page), cancellation);
    }

    /// <summary>
    /// A Bangla translation of one piece of text (up to about 1200 characters), or null (not enabled, not
    /// running, model missing, too slow, cancelled). The text goes only to the local Ollama.
    /// </summary>
    public static async Task<string?> TranslateAsync(string text, CancellationToken cancellation)
    {
        if (!AppServices.Settings.OllamaEnabled || CreateClient(TranslationTimeout) is not { } client)
        {
            return null;
        }
        return await client.ExplainAsync(TranslationPrompt.Build(text), cancellation, OllamaClient.TranslationTokens, OllamaClient.MaxTranslationLength);
    }
}
