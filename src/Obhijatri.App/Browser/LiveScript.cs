using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.Core;

namespace Obhijatri.App.Browser;

/// <summary>
/// A page script that follows a setting: added to a tab's engine while the setting is on (it then
/// runs in pages loaded from that point) and removed when it is off. The script is an embedded
/// resource that talks to nothing, so it needs no bridge token.
/// </summary>
internal sealed class LiveScript
{
    private readonly string _resourceName;
    private string? _scriptId;
    private bool _wanted;
    private bool _syncing;

    public LiveScript(string resourceName)
    {
        _resourceName = resourceName;
    }

    public void Apply(CoreWebView2 core, bool wanted)
    {
        _wanted = wanted;
        if (wanted != (_scriptId is not null) && !_syncing)
        {
            _ = SyncAsync(core);
        }
    }

    private async Task SyncAsync(CoreWebView2 core)
    {
        _syncing = true;
        try
        {
            // The setting may flip again while a script is being added or removed, so loop until
            // the script matches what is wanted now.
            while (_wanted != (_scriptId is not null))
            {
                if (_wanted)
                {
                    _scriptId = await core.AddScriptToExecuteOnDocumentCreatedAsync(PageBridge.ReadScript(_resourceName));
                }
                else
                {
                    core.RemoveScriptToExecuteOnDocumentCreated(_scriptId);
                    _scriptId = null;
                }
            }
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException)
        {
            // The engine is closing; the setting is applied again when a new tab starts.
        }
        finally
        {
            _syncing = false;
        }
    }
}
