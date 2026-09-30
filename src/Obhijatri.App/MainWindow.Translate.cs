using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Obhijatri.App.Browser;
using Obhijatri.App.Localization;
using Obhijatri.App.Services;
using Obhijatri.Safety.Translate;

namespace Obhijatri.App;

/// <summary>
/// "Translate page to Bangla". Two routes, each off until the person switches it on in Settings:
/// Google Translate (only the page address goes to Google; never for private windows, banking or
/// payment sites) and the local AI (Ollama), which translates the article in reader mode and keeps
/// the text on this computer. If both are on, the person chooses each time.
/// </summary>
public sealed partial class MainWindow
{
    private void UpdateTranslateMenu()
    {
        var google = AppServices.Settings.GoogleTranslateEnabled;
        var ollama = AppServices.Settings.OllamaEnabled;
        MenuTranslate.Visibility = google || ollama ? Visibility.Visible : Visibility.Collapsed;
        MenuTranslate.Text = Strings.Get("MenuTranslate");
    }

    private void MenuTranslate_Click(object sender, RoutedEventArgs e)
    {
        if (_activeTab is not { HasEngine: true } tab)
        {
            return;
        }

        var google = AppServices.Settings.GoogleTranslateEnabled;
        var ollama = AppServices.Settings.OllamaEnabled;
        if (google && ollama)
        {
            ShowTranslateChooser(tab);
        }
        else if (google)
        {
            _ = TranslateWithGoogleAsync(tab);
        }
        else if (ollama)
        {
            _ = OpenReaderAsync(startTranslation: true);
        }
    }

    private void ShowTranslateChooser(BrowserTab tab)
    {
        var panel = new StackPanel { Width = 340, Spacing = 8 };
        var flyout = new Flyout { Content = panel, Placement = FlyoutPlacementMode.BottomEdgeAlignedRight };

        var google = new Button { Content = Wrapped(Strings.Get("TranslateChooserGoogle")), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left };
        google.Click += (_, _) =>
        {
            flyout.Hide();
            _ = TranslateWithGoogleAsync(tab);
        };
        var local = new Button { Content = Wrapped(Strings.Get("TranslateChooserOllama")), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left };
        local.Click += (_, _) =>
        {
            flyout.Hide();
            _ = OpenReaderAsync(startTranslation: true);
        };
        panel.Children.Add(google);
        panel.Children.Add(local);
        flyout.ShowAt(MenuButton);
    }

    /// <summary>
    /// Sends this tab to Google's Bangla translation of the same page, after the first-use warning. Pages
    /// that must not go to Google are refused with the reason.
    /// </summary>
    private async Task TranslateWithGoogleAsync(BrowserTab tab)
    {
        if (!Uri.TryCreate(tab.Url, UriKind.Absolute, out var uri))
        {
            return;
        }

        var refusal = GoogleTranslate.Check(uri, IsPrivate);
        if (refusal != TranslateRefusal.None)
        {
            ShowInfo(Microsoft.UI.Xaml.Controls.InfoBarSeverity.Warning, "TranslateRefusedTitle", "TranslateRefused" + refusal);
            return;
        }

        if (!AppServices.Settings.GoogleTranslateWarned)
        {
            var dialog = new ContentDialog
            {
                XamlRoot = Root.XamlRoot,
                Title = Strings.Get("TranslateWarnTitle"),
                Content = Wrapped(Strings.Get("TranslateWarnMessage")),
                PrimaryButtonText = Strings.Get("TranslateWarnContinue"),
                CloseButtonText = Strings.Get("DialogCancel"),
                DefaultButton = ContentDialogButton.Close,
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }
            AppServices.Settings.GoogleTranslateWarned = true;
        }

        if (GoogleTranslate.BuildUrl(uri, IsPrivate) is { } target)
        {
            tab.Navigate(target.AbsoluteUri);
        }
    }
}
