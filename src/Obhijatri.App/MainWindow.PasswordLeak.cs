using Microsoft.UI.Xaml.Controls;
using Obhijatri.App.Localization;
using Obhijatri.App.Services;

namespace Obhijatri.App;

/// <summary>
/// Have I Been Pwned password leak check (Milestone 7): a submitted password's SHA-1 hash arrives
/// here from the page bridge (see Browser/PageBridge.cs and Web/password-leak.js); the network
/// lookup and the local suffix comparison both happen in Obhijatri.Safety/Privacy/HibpClient.cs.
/// </summary>
public sealed partial class MainWindow
{
    public async void OnPasswordHash(string sha1Hex, string host)
    {
        var count = await HibpService.CheckAsync(sha1Hex);
        if (count is not > 0)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = Content.XamlRoot,
            Title = Strings.Get("PasswordLeakWarningTitle"),
            Content = Strings.Format("PasswordLeakWarningMessageFormat", Formatting.Number(count.Value)),
            PrimaryButtonText = Strings.Get("DialogOk"),
            DefaultButton = ContentDialogButton.Primary,
        };
        await dialog.ShowAsync();
    }
}
