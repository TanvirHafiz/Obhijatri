using System.Security.Cryptography;
using System.Text;

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

/// <summary>
/// WebView2 in WinUI 3 does not pass keys such as Ctrl+T back to the app while a page has focus.
/// A small script, injected before any page script runs, catches only those combinations and
/// posts them to the app.
///
/// Security: every tab has its own random token, kept inside the script's closure where page
/// scripts cannot read it. Messages without the right token are ignored, so a page cannot trigger
/// browser actions by calling postMessage itself. Only real key presses (isTrusted) are sent.
/// </summary>
internal sealed class ShortcutBridge
{
    private static readonly Dictionary<string, BrowserShortcut> Commands =
        Enum.GetValues<BrowserShortcut>().ToDictionary(v => v.ToString(), v => v, StringComparer.Ordinal);

    private readonly byte[] _token;

    public ShortcutBridge()
    {
        _token = Encoding.ASCII.GetBytes(Convert.ToHexString(RandomNumberGenerator.GetBytes(16)));
    }

    public string Script => ScriptTemplate.Replace("__TOKEN__", Encoding.ASCII.GetString(_token), StringComparison.Ordinal);

    /// <summary>Returns the shortcut only if the message carries this tab's token.</summary>
    public BrowserShortcut? Parse(string? message)
    {
        if (message is null)
        {
            return null;
        }

        var colon = message.IndexOf(':', StringComparison.Ordinal);
        if (colon != _token.Length)
        {
            return null;
        }

        var token = Encoding.ASCII.GetBytes(message[..colon]);
        if (!CryptographicOperations.FixedTimeEquals(token, _token))
        {
            return null;
        }

        return Commands.TryGetValue(message[(colon + 1)..], out var shortcut) ? shortcut : null;
    }

    private const string ScriptTemplate = """
        (() => {
          const hostApi = window.chrome && window.chrome.webview;
          if (!hostApi) { return; }
          const post = hostApi.postMessage.bind(hostApi);
          const token = "__TOKEN__";
          const pick = (e) => {
            const k = e.key.length === 1 ? e.key.toLowerCase() : e.key;
            const ctrl = e.ctrlKey && !e.altKey && !e.metaKey;
            if (ctrl && !e.shiftKey) {
              if (k === "t") return "NewTab";
              if (k === "w" || k === "F4") return "CloseTab";
              if (k === "Tab") return "NextTab";
              if (k === "l") return "FocusAddressBar";
              if (k === "h") return "History";
              if (k === "j") return "Downloads";
              if (k === "d") return "Bookmark";
              if (k === ",") return "Settings";
            }
            if (ctrl && e.shiftKey) {
              if (k === "t") return "ReopenTab";
              if (k === "Tab") return "PreviousTab";
              if (k === "n") return "PrivateWindow";
              if (k === "b") return "ToggleBookmarkBar";
            }
            if (e.altKey && !e.ctrlKey && !e.shiftKey && !e.metaKey && k === "d") return "FocusAddressBar";
            if (!e.altKey && !e.ctrlKey && !e.shiftKey && !e.metaKey && k === "F6") return "FocusAddressBar";
            return null;
          };
          window.addEventListener("keydown", (e) => {
            if (!e.isTrusted) { return; }
            const command = pick(e);
            if (!command) { return; }
            e.preventDefault();
            e.stopImmediatePropagation();
            post(token + ":" + command);
          }, true);
        })();
        """;
}
