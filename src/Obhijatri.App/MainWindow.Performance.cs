using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Obhijatri.App.Browser;
using Obhijatri.App.Localization;
using Obhijatri.App.Services;
using Obhijatri.Core.Performance;

namespace Obhijatri.App;

/// <summary>
/// Milestone 8: tab sleeping, the memory meter, pinned tabs and the low data menu item.
/// </summary>
public sealed partial class MainWindow
{
    private static readonly TimeSpan SleepCheckInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MemoryRefreshInterval = TimeSpan.FromSeconds(5);

    /// <summary>
    /// The engine gives memory back gradually after a page is suspended (measured: about a fifth of
    /// it after 8 seconds, nearly all after 40), so the "saved" figure is taken this long afterwards.
    /// </summary>
    private static readonly TimeSpan SleepSettleTime = TimeSpan.FromSeconds(30);

    private DispatcherQueueTimer? _sleepTimer;
    private DispatcherQueueTimer? _memoryTimer;
    private bool _sleepPassRunning;
    private bool _memoryRefreshRunning;
    private MemorySample? _lastSample;

    private void InitializePerformance()
    {
        _sleepTimer = DispatcherQueue.CreateTimer();
        _sleepTimer.Interval = SleepCheckInterval;
        _sleepTimer.Tick += (_, _) => _ = SleepIdleTabsAsync(ignoreIdle: false);
        _sleepTimer.Start();

        // The meter only measures while this window is the one being used.
        _memoryTimer = DispatcherQueue.CreateTimer();
        _memoryTimer.Interval = MemoryRefreshInterval;
        _memoryTimer.Tick += (_, _) => _ = RefreshMemoryAsync();
        _memoryTimer.Start();
        Activated += (_, args) =>
        {
            if (args.WindowActivationState == WindowActivationState.Deactivated)
            {
                _memoryTimer?.Stop();
            }
            else
            {
                _memoryTimer?.Start();
                _ = RefreshMemoryAsync();
            }
        };

        RamButton.Visibility = AppServices.Settings.ShowMemoryMeter ? Visibility.Visible : Visibility.Collapsed;
        SetLabel(RamButton, "RamMeterTooltip");
        MenuLowData.IsChecked = AppServices.Settings.LowDataMode;
        MenuLowData.Text = Strings.Get("MenuLowData");
        UpdateTranslateMenu();
    }

    private void StopPerformanceTimers()
    {
        _sleepTimer?.Stop();
        _memoryTimer?.Stop();
    }

    // ---- Tab sleeping ----

    /// <summary>
    /// Puts idle background tabs to sleep. With <paramref name="ignoreIdle"/> every tab that is
    /// allowed to sleep does so now (the "sleep now" button). Returns how many slept and the
    /// memory they gave back, measured before and after.
    /// </summary>
    internal async Task<(int Slept, long SavedBytes)> SleepIdleTabsAsync(bool ignoreIdle)
    {
        var minutes = AppServices.Settings.TabSleepMinutes;
        if (_sleepPassRunning || (minutes == 0 && !ignoreIdle))
        {
            return (0, 0);
        }

        var now = TimeProvider.System.GetUtcNow();
        var candidates = _tabs.Where(t => t.HasEngine && TabSleepPolicy.ShouldSleep(
            ignoreIdle ? 1 : minutes,
            ignoreIdle ? TimeSpan.FromMinutes(1) : now - t.LastActiveAt,
            isActive: t == _activeTab,
            isPinned: t.IsPinned,
            isPlayingAudio: t.IsPlayingAudio,
            isLoading: t.IsLoading,
            isSleeping: t.IsSleeping)).ToList();
        if (candidates.Count == 0)
        {
            return (0, 0);
        }

        _sleepPassRunning = true;
        try
        {
            var before = await MemoryMeter.SampleAsync(_tabs);
            var slept = new List<BrowserTab>();
            foreach (var tab in candidates)
            {
                if (!await tab.TrySleepAsync(() => tab == _activeTab))
                {
                    continue;
                }
                if (tab == _activeTab)
                {
                    // The person switched to it while it was being suspended.
                    tab.Wake();
                }
                else
                {
                    slept.Add(tab);
                }
            }
            if (slept.Count == 0)
            {
                return (0, 0);
            }

            await Task.Delay(SleepSettleTime);
            var after = await MemoryMeter.SampleAsync(_tabs);

            // Credit a tab only when it can still be found afterwards, so a measurement that lost
            // track of it is never counted as memory saved.
            long saved = 0;
            foreach (var tab in slept)
            {
                if (after.PerTab.TryGetValue(tab, out var now2))
                {
                    saved += Math.Max(0, before.BytesFor(tab) - now2);
                }
            }
            AppServices.MemorySaved.Add(saved);
            ApplySample(after);
            return (slept.Count, saved);
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException)
        {
            return (0, 0);
        }
        finally
        {
            _sleepPassRunning = false;
        }
    }

    // ---- Memory meter ----

    private async Task RefreshMemoryAsync()
    {
        if (_memoryRefreshRunning)
        {
            return;
        }

        _memoryRefreshRunning = true;
        try
        {
            ApplySample(await MemoryMeter.SampleAsync(_tabs));
        }
        finally
        {
            _memoryRefreshRunning = false;
        }
    }

    private void ApplySample(MemorySample sample)
    {
        _lastSample = sample;
        foreach (var tab in _tabs)
        {
            tab.MemoryBytes = sample.BytesFor(tab);
        }
        RamText.Text = Formatting.Bytes(sample.TotalBytes);
    }

    private async void RamButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshMemoryAsync();
        var sample = _lastSample;
        if (sample is null)
        {
            return;
        }

        var panel = new StackPanel { Width = 320, Spacing = 8 };
        panel.Children.Add(new TextBlock
        {
            Text = Strings.Get("RamTitle"),
            Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"],
        });
        panel.Children.Add(new TextBlock
        {
            Text = Strings.Format("RamTotalFormat", Formatting.Bytes(sample.TotalBytes)),
            Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
        });
        panel.Children.Add(new TextBlock { Text = Strings.Format("RamShellFormat", Formatting.Bytes(sample.ShellBytes)) });
        panel.Children.Add(new TextBlock { Text = Strings.Format("RamEngineFormat", Formatting.Bytes(sample.EngineBytes)) });
        panel.Children.Add(new TextBlock { Text = Strings.Format("RamSleepingFormat", Formatting.Number(_tabs.Count(t => t.IsSleeping))) });
        panel.Children.Add(new TextBlock
        {
            Text = Strings.Format("RamSavedTodayFormat", Formatting.Bytes(AppServices.MemorySaved.TodayBytes)),
            Style = (Style)Application.Current.Resources["SuccessCaptionTextBlockStyle"],
        });

        var sleepNow = new Button { Content = Strings.Get("RamSleepNow"), HorizontalAlignment = HorizontalAlignment.Left };
        var flyout = new Flyout { Content = panel, Placement = Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.BottomEdgeAlignedRight };
        sleepNow.Click += async (_, _) =>
        {
            flyout.Hide();
            await SleepIdleTabsAsync(ignoreIdle: true);
        };
        panel.Children.Add(sleepNow);
        panel.Children.Add(new TextBlock
        {
            Text = Strings.Get("RamHint"),
            TextWrapping = TextWrapping.Wrap,
            Style = (Style)Application.Current.Resources["SecondaryCaptionTextBlockStyle"],
        });
        flyout.ShowAt(RamButton);
    }

    // ---- Pinned tabs ----

    private void TabPin_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is BrowserTab tab)
        {
            TogglePin(tab);
        }
    }

    /// <summary>Pinned tabs sit at the start of the strip; a pinned tab is never put to sleep.</summary>
    private void TogglePin(BrowserTab tab)
    {
        var index = _tabs.IndexOf(tab);
        if (index < 0)
        {
            return;
        }

        tab.IsPinned = !tab.IsPinned;
        var target = _tabs.Count(t => t.IsPinned && t != tab);
        if (target != index)
        {
            _tabs.Move(index, target);
        }
        ScheduleSessionSave();
    }

    // ---- Low data mode ----

    private void MenuLowData_Click(object sender, RoutedEventArgs e) =>
        AppServices.Settings.LowDataMode = MenuLowData.IsChecked;
}
