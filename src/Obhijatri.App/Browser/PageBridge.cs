using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Obhijatri.App.Localization;
using Obhijatri.Bangla.Phonetic;

namespace Obhijatri.App.Browser;

public enum BrowserShortcut
{
    NewTab,
    CloseTab,
    ReopenTab,
    NextTab,
    PreviousTab,
    FocusAddressBar,
    History,
    Downloads,
    Bookmark,
    ToggleBookmarkBar,
    PrivateWindow,
    Settings,
}

/// <summary>A verified message from this tab's injected script.</summary>
internal sealed record PageMessage(string Command, string Payload)
{
    public BrowserShortcut? Shortcut =>
        Enum.TryParse<BrowserShortcut>(Command, ignoreCase: false, out var shortcut) && Enum.IsDefined(shortcut) ? shortcut : null;
}

/// <summary>
/// The script injected into every frame of a tab (before page scripts run) and the messages it
/// exchanges with the app: browser shortcuts (WebView2 in WinUI 3 does not pass keys such as
/// Ctrl+T to the app while a page has focus) and Bangla phonetic typing.
///
/// Security: two random secrets per tab.
///  - The outgoing token proves a message came from our script. It stays inside the script's
///    closure and is never sent to the page, so page scripts cannot forge commands.
///  - The incoming prefix marks messages from the app. Page scripts can read those messages, so
///    only harmless data (typing on/off, suggestions) is ever sent that way.
/// </summary>
internal sealed class PageBridge
{
    private static readonly Lazy<string> Template = new(BuildTemplate);

    private readonly byte[] _outToken;
    private readonly string _inToken;

    public PageBridge()
    {
        _outToken = Encoding.ASCII.GetBytes(Convert.ToHexString(RandomNumberGenerator.GetBytes(16)));
        _inToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
    }

    public string Script => Template.Value
        .Replace("__OUT_TOKEN__", Encoding.ASCII.GetString(_outToken), StringComparison.Ordinal)
        .Replace("__IN_TOKEN__", _inToken, StringComparison.Ordinal);

    /// <summary>Returns the message only if it carries this tab's outgoing token.</summary>
    public PageMessage? Parse(string? message)
    {
        if (message is null)
        {
            return null;
        }

        var colon = message.IndexOf(':', StringComparison.Ordinal);
        if (colon != _outToken.Length
            || !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(message[..colon]), _outToken))
        {
            return null;
        }

        var rest = message[(colon + 1)..];
        var split = rest.IndexOf(':', StringComparison.Ordinal);
        return split < 0 ? new PageMessage(rest, string.Empty) : new PageMessage(rest[..split], rest[(split + 1)..]);
    }

    /// <summary>A message for the page script. Never put anything secret in it.</summary>
    public string Compose(string command, string payload) => $"{_inToken}:{command}:{payload}";

    /// <summary>Wraps all parts in one function so nothing leaks into the page's global scope.</summary>
    private static string BuildTemplate()
    {
        var labels = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["on"] = Strings.Get("PhoneticPageOn"),
            ["off"] = Strings.Get("PhoneticPageOff"),
        });

        var script = new StringBuilder();
        script.Append("(() => {\n");
        script.Append("const RULES = ").Append(Compact(AvroPhonetic.RulesJson)).Append(";\n"); // not-ui
        script.Append("const LABELS = ").Append(labels).Append(";\n"); // not-ui
        script.Append(ReadScript("bridge-prelude.js")).Append('\n');
        script.Append(ReadScript("bridge-shortcuts.js")).Append('\n');
        script.Append(ReadScript("avro-phonetic.js")).Append('\n');
        script.Append(ReadScript("phonetic-typing.js")).Append('\n');
        script.Append(ReadScript("password-leak.js")).Append('\n');
        script.Append(ReadScript("clipboard-guard.js")).Append('\n');
        script.Append("})();\n");
        return script.ToString();
    }

    /// <summary>Removes whitespace from the rules JSON (Bangla text stays as is) to keep the script small.</summary>
    private static string Compact(string json)
    {
        using var document = JsonDocument.Parse(json);
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions
        {
            // Safe here: the result is JavaScript source in our own script, not HTML.
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        }))
        {
            document.WriteTo(writer);
        }
        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    internal static string ReadScript(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Obhijatri.App.Web." + name)
                           ?? throw new InvalidOperationException("Missing page script " + name);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
