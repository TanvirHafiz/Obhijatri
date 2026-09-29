#if DEBUG
using Microsoft.UI.Xaml.Media.Imaging;
using Obhijatri.Core;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Obhijatri.App;

/// <summary>
/// Developer-only screenshots of the window chrome itself (Debug builds, --theme-selftest and
/// --newtab-selftest): the tab strip, toolbar and built-in pages are native XAML, not a web page, so
/// BrowserTab's CapturePreviewAsync (which only captures the WebView2 content) cannot show them.
/// Renders the window's root element instead. Saves to %LOCALAPPDATA%\Obhijatri\logs\benchmark and exits.
/// </summary>
public sealed partial class MainWindow
{
    internal async Task RunThemeSelfTestAsync()
    {
        try
        {
            var tab = _activeTab!;
            for (var wait = 0; tab.WebView?.CoreWebView2 is null && wait < 150; wait++)
            {
                await Task.Delay(200);
            }
            await Task.Delay(1000);
            await RenderRootToPngAsync("theme.png");
        }
        catch (Exception ex)
        {
            await LogSelfTestErrorAsync("theme-selftest-error.txt", ex);
        }
        finally
        {
            Microsoft.UI.Xaml.Application.Current.Exit();
        }
    }

    /// <summary>The new tab page is the active tab of a fresh private window: render it.</summary>
    internal async Task RunNewTabSelfTestAsync()
    {
        try
        {
            await Task.Delay(2500);
            // An off-screen render has no Mica backdrop, so a dark theme would draw light text on white.
            Root.RequestedTheme = Microsoft.UI.Xaml.ElementTheme.Light;
            await Task.Delay(500);
            await RenderRootToPngAsync("newtab.png");
        }
        catch (Exception ex)
        {
            await LogSelfTestErrorAsync("newtab-selftest-error.txt", ex);
        }
        finally
        {
            Microsoft.UI.Xaml.Application.Current.Exit();
        }
    }

    private async Task RenderRootToPngAsync(string fileName)
    {
        var bitmap = new RenderTargetBitmap();
        await bitmap.RenderAsync(Root);
        var pixelBuffer = await bitmap.GetPixelsAsync();
        var pixels = new byte[pixelBuffer.Length];
        using (var reader = DataReader.FromBuffer(pixelBuffer))
        {
            reader.ReadBytes(pixels);
        }

        var folder = System.IO.Path.Combine(AppPaths.LogFolder, "benchmark");
        Directory.CreateDirectory(folder);
        await using var file = File.Create(System.IO.Path.Combine(folder, fileName));
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, file.AsRandomAccessStream());
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
            (uint)bitmap.PixelWidth, (uint)bitmap.PixelHeight, 96, 96, pixels);
        await encoder.FlushAsync();
    }

    private static async Task LogSelfTestErrorAsync(string fileName, Exception ex)
    {
        Directory.CreateDirectory(AppPaths.LogFolder);
        await File.WriteAllTextAsync(System.IO.Path.Combine(AppPaths.LogFolder, fileName), ex.ToString()); // not-ui
    }
}
#endif
