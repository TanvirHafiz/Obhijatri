using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Obhijatri.AI.Ollama;

/// <summary>
/// Talks to an Ollama server running on this computer (layer 2 of "এটা কি প্রতারণা?"). Page text is
/// sent only to it, so the address must be a loopback one: anything else is refused, which keeps the
/// rule that page content never leaves the PC. Every failure (not running, wrong model, slow, odd
/// answer) gives null, and the caller then shows the rule based result alone.
/// </summary>
public sealed partial class OllamaClient
{
    public static readonly Uri DefaultEndpoint = new("http://127.0.0.1:11434/");
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(20);

    public const int MaxResponseLength = 1500;

    /// <summary>The longest answer accepted for a translation of one piece of text.</summary>
    public const int MaxTranslationLength = 3000;

    public const int ExplanationTokens = 320;
    public const int TranslationTokens = 900;
    public const string DefaultModel = "llama3.2";
    private const int MaxResponseBytes = 256 * 1024;

    private readonly HttpClient _http;
    private readonly Uri _endpoint;
    private readonly string _model;
    private readonly TimeSpan _timeout;

    /// <exception cref="ArgumentException">The address is not plain http on a loopback address, or the model name is not valid.</exception>
    public OllamaClient(HttpClient http, Uri endpoint, string model, TimeSpan? timeout = null)
    {
        if (!IsLoopbackHttp(endpoint))
        {
            throw new ArgumentException("Ollama must be on this computer (a loopback http address).", nameof(endpoint));
        }
        if (!IsValidModelName(model))
        {
            throw new ArgumentException("Not a valid model name.", nameof(model));
        }

        _http = http;
        _endpoint = endpoint;
        _model = model;
        _timeout = timeout ?? DefaultTimeout;
    }

    /// <summary>Plain http to 127.0.0.1, ::1 or localhost. Never https to a remote host, never a name that resolves elsewhere.</summary>
    public static bool IsLoopbackHttp(Uri? endpoint) =>
        endpoint is { IsAbsoluteUri: true, Scheme: "http" }
        && (endpoint.IsLoopback || endpoint.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        && string.IsNullOrEmpty(endpoint.UserInfo);

    public static bool IsValidModelName(string? model) => model is not null && ModelName().IsMatch(model);

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._:/-]{0,79}$")]
    private static partial Regex ModelName();

    /// <summary>The names of the installed models, or null if Ollama does not answer within <paramref name="timeout"/>.</summary>
    public async Task<IReadOnlyList<string>?> ListModelsAsync(TimeSpan timeout, CancellationToken cancellation = default)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        limit.CancelAfter(timeout);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(_endpoint, "api/tags"));
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, limit.Token);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using var document = JsonDocument.Parse(await ReadLimitedAsync(response, limit.Token));
            if (!document.RootElement.TryGetProperty("models", out var models) || models.ValueKind != JsonValueKind.Array)
            {
                return [];
            }
            return models.EnumerateArray()
                .Select(m => m.ValueKind == JsonValueKind.Object && m.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null)
                .Where(n => n is not null && IsValidModelName(n))
                .Select(n => n!)
                .ToList();
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or InvalidOperationException or IOException)
        {
            return null;
        }
    }

    /// <summary>
    /// Asks the model. Returns the cleaned answer, or null if Ollama is not running, the model is
    /// missing, it takes longer than the timeout (20 seconds), or the answer is empty or unusable.
    /// </summary>
    public async Task<string?> ExplainAsync(string prompt, CancellationToken cancellation = default, int maxTokens = ExplanationTokens, int maxLength = MaxResponseLength)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        limit.CancelAfter(_timeout);
        try
        {
            var body = JsonSerializer.Serialize(new
            {
                model = _model,
                prompt,
                stream = false,
                options = new { temperature = 0.2, num_predict = maxTokens },
            });
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_endpoint, "api/generate"))
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, limit.Token);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using var document = JsonDocument.Parse(await ReadLimitedAsync(response, limit.Token));
            var text = document.RootElement.ValueKind == JsonValueKind.Object
                       && document.RootElement.TryGetProperty("response", out var answer)
                       && answer.ValueKind == JsonValueKind.String
                ? answer.GetString()
                : null;
            var cleaned = Clean(text, maxLength);
            return cleaned.Length == 0 ? null : cleaned;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or InvalidOperationException or IOException)
        {
            return null;
        }
    }

    private static async Task<byte[]> ReadLimitedAsync(HttpResponseMessage response, CancellationToken cancellation)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellation);
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellation)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > MaxResponseBytes)
            {
                throw new IOException("Answer too large.");
            }
        }
        return buffer.ToArray();
    }

    /// <summary>
    /// The answer is shown as plain text: markdown marks, control and direction-changing characters
    /// are removed and the length is capped, so a model that has been steered by the page cannot
    /// smuggle formatting or hidden text into the window.
    /// </summary>
    internal static string Clean(string? text, int maxLength = MaxResponseLength)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var result = new StringBuilder(Math.Min(text.Length, maxLength));
        var lastWasSpace = true;
        foreach (var c in text)
        {
            if (result.Length >= maxLength)
            {
                break;
            }
            if (c is '*' or '#' or '`')
            {
                continue;
            }
            if (char.IsWhiteSpace(c))
            {
                if (!lastWasSpace)
                {
                    result.Append(' ');
                    lastWasSpace = true;
                }
                continue;
            }
            if (char.IsControl(c) || c is >= '‪' and <= '‮' or >= '⁦' and <= '⁩' or '​' or '﻿')
            {
                continue;
            }
            result.Append(c);
            lastWasSpace = false;
        }
        return result.ToString().Trim();
    }
}
