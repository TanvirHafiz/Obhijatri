using Microsoft.UI.Xaml;
using Microsoft.Web.WebView2.Core;
using Obhijatri.App.Downloads;
using Obhijatri.App.Services;

namespace Obhijatri.App;

public sealed partial class MainWindow
{
    private DownloadList _downloads = null!;

    private void InitializeDownloads()
    {
        // Private windows keep their own list, so nothing from them shows up in normal windows.
        _downloads = IsPrivate ? new DownloadList() : AppServices.Downloads;
        DownloadsListView.ItemsSource = _downloads;
        _downloads.CollectionChanged += (_, _) => UpdateDownloadsEmptyState();
        UpdateDownloadsEmptyState();
    }

    public void OnDownloadStarting(CoreWebView2DownloadStartingEventArgs args)
    {
        // Our own panel replaces the engine's download popup.
        args.Handled = true;
        _downloads.AddNewest(new DownloadItem(args.DownloadOperation));
        ShowDownloads();
    }

    private void ShowDownloads() => DownloadsFlyout.ShowAt(DownloadsButton);

    private void UpdateDownloadsEmptyState()
    {
        var empty = _downloads.Count == 0;
        DownloadsEmptyText.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        DownloadsListView.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
    }

    private void DownloadOpen_Click(object sender, RoutedEventArgs e) => (Tag(sender))?.Open();

    private void DownloadShowInFolder_Click(object sender, RoutedEventArgs e) => (Tag(sender))?.ShowInFolder();

    private void DownloadCancel_Click(object sender, RoutedEventArgs e) => (Tag(sender))?.Cancel();

    private static DownloadItem? Tag(object sender) => (sender as FrameworkElement)?.Tag as DownloadItem;
}
