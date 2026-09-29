#if DEBUG
using Microsoft.UI.Xaml.Media.Imaging;
using Obhijatri.Core;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Obhijatri.App;

/// <summary>
/// Developer-only screenshot of the window chrome itself (Debug builds, --theme-selftest): the tab
/// strip and toolbar are native XAML, not a web page, so BrowserTab's CapturePreviewAsync (which
/// only captures the WebView2 content) cannot show them. Renders the window's root element instead.
/// Saves to %LOCALAPPDATA%\Obhijatri\logs\benchmark\theme.png and exits.
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
            await using var file = File.Create(System.IO.Path.Combine(folder, "theme.png"));
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, file.AsRandomAccessStream());
            encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
                (uint)bitmap.PixelWidth, (uint)bitmap.PixelHeight, 96, 96, pixels);
            await encoder.FlushAsync();
        }
        catch (Exception ex)
        {
            Directory.CreateDirectory(AppPaths.LogFolder);
            await File.WriteAllTextAsync(System.IO.Path.Combine(AppPaths.LogFolder, "theme-selftest-error.txt"), ex.ToString()); // not-ui
        }
        finally
        {
            Microsoft.UI.Xaml.Application.Current.Exit();
        }
    }
}
#endif
