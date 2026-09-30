#if DEBUG
using Microsoft.UI.Xaml;
using Obhijatri.Core;

namespace Obhijatri.App;

/// <summary>Developer-only screenshot of the About page (Debug builds, --about-selftest): logs\benchmark\about.png.</summary>
public sealed partial class MainWindow
{
    internal async Task RunAboutSelfTestAsync()
    {
        try
        {
            OpenAbout();
            await Task.Delay(3500);
            Root.RequestedTheme = ElementTheme.Light;
            await Task.Delay(400);
            await RenderRootToPngAsync("about.png");
        }
        catch (Exception ex)
        {
            await LogSelfTestErrorAsync("about-selftest-error.txt", ex);
        }
        finally
        {
            Application.Current.Exit();
        }
    }
}
#endif
