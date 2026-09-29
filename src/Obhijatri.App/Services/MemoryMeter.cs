using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.Core;
using Obhijatri.App.Browser;

namespace Obhijatri.App.Services;

/// <summary>What the browser uses right now, in bytes of private memory.</summary>
internal sealed record MemorySample(long ShellBytes, long EngineBytes, IReadOnlyDictionary<BrowserTab, long> PerTab)
{
    public long TotalBytes => ShellBytes + EngineBytes;

    public long BytesFor(BrowserTab tab) => PerTab.GetValueOrDefault(tab);
}

/// <summary>
/// Measures the app's own process plus every engine process of the shared WebView2 environment.
/// A renderer process is charged to the tab whose page (or a frame inside it) it is running; if
/// several tabs share one process its memory is split between them. Shared helper processes
/// (browser, GPU, network) count towards the total only.
/// </summary>
internal static class MemoryMeter
{
    public static async Task<MemorySample> SampleAsync(IEnumerable<BrowserTab> tabs)
    {
        var shell = ProcessMemory.PrivateBytes(Environment.ProcessId);
        long engine = 0;
        var perTab = new Dictionary<BrowserTab, long>();

        try
        {
            var environment = await WebViewEnvironment.GetAsync();
            var infos = await environment.GetProcessExtendedInfosAsync();

            var mainFrames = new Dictionary<uint, BrowserTab>();
            foreach (var tab in tabs)
            {
                if (tab.MainFrameId is { } id)
                {
                    mainFrames[id] = tab;
                }
            }

            foreach (var info in infos)
            {
                var bytes = ProcessMemory.PrivateBytes(info.ProcessInfo.ProcessId);
                engine += bytes;
                if (info.ProcessInfo.Kind != CoreWebView2ProcessKind.Renderer || bytes == 0)
                {
                    continue;
                }

                var owners = new HashSet<BrowserTab>();
                foreach (var frame in info.AssociatedFrameInfos)
                {
                    var top = frame;
                    while (top.ParentFrameInfo is { } parent)
                    {
                        top = parent;
                    }
                    if (mainFrames.TryGetValue(top.FrameId, out var owner))
                    {
                        owners.Add(owner);
                    }
                }

                foreach (var owner in owners)
                {
                    perTab[owner] = perTab.GetValueOrDefault(owner) + bytes / owners.Count;
                }
            }
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or ArgumentException)
        {
            // The engine is starting or shutting down; report what could be measured.
        }

        return new MemorySample(shell, engine, perTab);
    }
}
