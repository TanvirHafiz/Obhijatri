using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Obhijatri.App.Localization;
using Obhijatri.Core.Storage;

namespace Obhijatri.App.Views;

public sealed partial class HistoryRow
{
    internal HistoryRow(HistoryEntry entry)
    {
        Id = entry.Id;
        Url = entry.Url;
        Title = string.IsNullOrWhiteSpace(entry.Title) ? entry.Url : entry.Title;
        TimeText = Formatting.DateTime(entry.VisitedAt);
    }

    public long Id { get; }
    public string Url { get; }
    public string Title { get; }
    public string TimeText { get; }
}

/// <summary>The built-in history page.</summary>
public sealed partial class HistoryView : UserControl
{
    private readonly HistoryStore _history;
    private readonly Action<string> _openInNewTab;
    private readonly Func<TimeSpan?, Task> _clearEngineHistory;

    internal HistoryView(HistoryStore history, Action<string> openInNewTab, Func<TimeSpan?, Task> clearEngineHistory)
    {
        _history = history;
        _openInNewTab = openInNewTab;
        _clearEngineHistory = clearEngineHistory;
        InitializeComponent();

        HeaderText.Text = Strings.Get("HistoryTitle");
        SearchBox.PlaceholderText = Strings.Get("HistorySearchPlaceholder");
        ClearButton.Content = Strings.Get("HistoryClearButton");
        ClearHourItem.Text = Strings.Get("HistoryClearHour");
        ClearDayItem.Text = Strings.Get("HistoryClearDay");
        ClearAllItem.Text = Strings.Get("HistoryClearAll");

        Refresh();
    }

    public void Refresh()
    {
        var rows = _history.Search(SearchBox.Text).Select(e => new HistoryRow(e)).ToList();
        EntryList.ItemsSource = rows;
        EmptyText.Text = Strings.Get(string.IsNullOrWhiteSpace(SearchBox.Text) ? "HistoryEmpty" : "HistoryNoMatches");
        EmptyText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => Refresh();

    private void EntryList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is HistoryRow row)
        {
            _openInNewTab(row.Url);
        }
    }

    private void DeleteEntry_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is HistoryRow row)
        {
            _history.Delete(row.Id);
            Refresh();
        }
    }

    private async void ClearHour_Click(object sender, RoutedEventArgs e) =>
        await ClearAsync(TimeSpan.FromHours(1), "HistoryConfirmHour");

    private async void ClearDay_Click(object sender, RoutedEventArgs e) =>
        await ClearAsync(TimeSpan.FromDays(1), "HistoryConfirmDay");

    private async void ClearAll_Click(object sender, RoutedEventArgs e) =>
        await ClearAsync(null, "HistoryConfirmAll");

    private async Task ClearAsync(TimeSpan? span, string confirmKey)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = Strings.Get("HistoryConfirmTitle"),
            Content = Strings.Get(confirmKey),
            PrimaryButtonText = Strings.Get("HistoryConfirmYes"),
            CloseButtonText = Strings.Get("DialogCancel"),
            DefaultButton = ContentDialogButton.Close,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        if (span is TimeSpan s)
        {
            _history.ClearLast(s);
        }
        else
        {
            _history.ClearAll();
        }

        // Also clear the engine's own record (used for visited-link colours).
        await _clearEngineHistory(span);
        Refresh();
    }
}
