using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Obhijatri.Bangla.Bijoy;

namespace Obhijatri.App.Browser;

/// <summary>Bijoy (SutonnyMJ) text on the page: noticing it and converting it to Unicode Bangla (Milestone 9b).</summary>
public sealed partial class BrowserTab
{
    private const string UnicodeFontStack = "\"Nirmala UI\", \"Noto Sans Bengali\", Vrinda, sans-serif"; // not-ui

    private bool _hasBijoyText;

    /// <summary>True when the page has text in a Bijoy family font that can be converted. Reset on each navigation.</summary>
    public bool HasBijoyText { get => _hasBijoyText; private set => Set(ref _hasBijoyText, value); }

    /// <summary>
    /// Converts the page's Bijoy font text to Unicode Bangla, in place, and returns how many pieces of
    /// text were changed (0 if there was nothing to convert or the page could not be reached). The
    /// conversion is done here, in one tested place; the page only lists its text and takes the result.
    /// </summary>
    public async Task<int> ConvertBijoyAsync()
    {
        if (WebView?.CoreWebView2 is not { } core)
        {
            return 0;
        }

        try
        {
            var collected = await core.ExecuteScriptAsync(PageBridge.ReadScript("bijoy-collect.js"));
            var texts = JsonSerializer.Deserialize<string[]>(JsonSerializer.Deserialize<string>(collected) ?? "[]") ?? [];
            if (texts.Length == 0)
            {
                HasBijoyText = false;
                return 0;
            }

            var converter = BijoyConverter.Instance;
            var converted = await Task.Run(() => texts.Select(converter.ToUnicode).ToArray());

            // The converted text goes back as a JSON string array (which is also valid script), so
            // nothing from the page can end up being run as code.
            var apply = "(() => { const nodes = window.__obhijatriBijoy; const texts = " + JsonSerializer.Serialize(converted) + ";" // not-ui
                        + " if (!Array.isArray(nodes) || nodes.length !== texts.length) { return 0; }" // not-ui
                        + " for (let i = 0; i < nodes.length; i++) {" // not-ui
                        + "  nodes[i].data = texts[i];" // not-ui
                        + "  const p = nodes[i].parentElement;" // not-ui
                        + "  if (p) { p.style.setProperty('font-family', " + JsonSerializer.Serialize(UnicodeFontStack) + ", 'important'); }" // not-ui
                        + " }" // not-ui
                        + " window.__obhijatriBijoy = null; return nodes.length; })()"; // not-ui
            var applied = await core.ExecuteScriptAsync(apply);
            var count = int.TryParse(applied, out var n) ? n : 0;
            if (count > 0)
            {
                HasBijoyText = false;
            }
            return count;
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or JsonException)
        {
            return 0;
        }
    }
}
