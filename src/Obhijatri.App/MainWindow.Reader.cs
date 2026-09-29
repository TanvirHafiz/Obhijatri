using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Obhijatri.App.Browser;
using Obhijatri.App.Localization;
using Obhijatri.App.Views;
using Obhijatri.Core.Reader;

namespace Obhijatri.App;

/// <summary>
/// Reader mode: the page's article text in a clean view over the page, optionally read aloud. The
/// view covers the tab's page while it is open and closes when the tab changes or navigates.
/// </summary>
public sealed partial class MainWindow
{
    private ReaderView? _readerView;

    private void UpdateReaderButton()
    {
        ReaderButton.IsEnabled = _activeTab is { HasEngine: true } tab && tab.Url.StartsWith("http", StringComparison.OrdinalIgnoreCase);
        SetLabel(ReaderButton, _readerView is null ? "ReaderButtonTooltip" : "ReaderClose");
    }

    private async void ReaderButton_Click(object sender, RoutedEventArgs e)
    {
        if (_readerView is not null)
        {
            CloseReader();
            return;
        }
        await OpenReaderAsync();
    }

    private async Task OpenReaderAsync()
    {
        if (_activeTab is not { HasEngine: true } tab || tab.WebView?.CoreWebView2 is not { } core)
        {
            return;
        }

        string? json;
        try
        {
            var raw = await core.ExecuteScriptAsync(PageBridge.ReadScript("reader-extract.js"));
            json = JsonSerializer.Deserialize<string>(raw);
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or JsonException)
        {
            json = null;
        }

        // The page's own scripts run in the same world as ours, so the result is checked and capped.
        var article = ReaderArticle.Parse(json);
        if (article is null)
        {
            ShowInfo(Microsoft.UI.Xaml.Controls.InfoBarSeverity.Informational, "ReaderUnavailableTitle", "ReaderUnavailableMessage");
            return;
        }
        if (_activeTab != tab || _readerView is not null)
        {
            return; // the person moved on while the page was being read
        }

        StatusInfoBar.IsOpen = false;
        _readerView = new ReaderView(article, CloseReader);
        ContentHost.Children.Add(_readerView);
        ShowOnlyActiveContent();
        UpdateReaderButton();
    }

    // ---- Bijoy (SutonnyMJ) text ----

    private void UpdateBijoyChip()
    {
        var show = _activeTab is { HasBijoyText: true };
        BijoyChipButton.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        if (show)
        {
            BijoyChipText.Text = Strings.Get("BijoyChipText");
            SetLabel(BijoyChipButton, "BijoyChipTooltip");
        }
    }

    private async void BijoyChipButton_Click(object sender, RoutedEventArgs e)
    {
        if (_activeTab is not { HasBijoyText: true } tab)
        {
            return;
        }

        var count = await tab.ConvertBijoyAsync();
        if (count > 0)
        {
            ShowInfo(Microsoft.UI.Xaml.Controls.InfoBarSeverity.Success, "BijoyConvertedTitle",
                Strings.Format("BijoyConvertedFormat", Formatting.Number(count)), messageIsKey: false);
        }
        else
        {
            ShowInfo(Microsoft.UI.Xaml.Controls.InfoBarSeverity.Informational, "BijoyNothingTitle", "BijoyNothingMessage");
        }
    }

    private void CloseReader()
    {
        if (_readerView is not { } view)
        {
            return;
        }

        _readerView = null;
        ContentHost.Children.Remove(view);
        view.Dispose();
        ShowOnlyActiveContent();
        UpdateReaderButton();
    }
}
