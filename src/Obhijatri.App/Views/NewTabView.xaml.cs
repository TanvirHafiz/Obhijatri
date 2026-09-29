using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Obhijatri.App.Localization;
using Obhijatri.App.Services;
using Obhijatri.Bangla.Calendars;
using Obhijatri.Bangla.Prayer;
using Obhijatri.Core.Storage;

namespace Obhijatri.App.Views;

/// <summary>
/// The new tab page: today's date in the Bangla, English and Hijri calendars, prayer times for a
/// chosen district, and tiles for frequently visited sites. Everything is worked out on this
/// computer; nothing is fetched. Built as a native view (not web content), so a new tab needs no
/// engine process and uses almost no memory.
/// </summary>
public sealed partial class NewTabView : UserControl
{
    private const int TileCount = 8;
    private const double TileWidth = 132;

    /// <summary>Front page addresses of well known sites, used to fill the tiles when there is little history.</summary>
    private static readonly (string LabelKey, string Url)[] DefaultSites =
    [
        ("SpeedDialProthomAlo", "https://www.prothomalo.com/"),
        ("SpeedDialBkash", "https://www.bkash.com/"),
        ("SpeedDialDaraz", "https://www.daraz.com.bd/"),
        ("SpeedDialFacebook", "https://www.facebook.com/"),
        ("SpeedDialWikipedia", "https://bn.wikipedia.org/"),
        ("SpeedDialDailyStar", "https://www.thedailystar.net/"),
        ("SpeedDialBdnews24", "https://bdnews24.com/"),
        ("SpeedDialGoogle", "https://www.google.com/"),
    ];

    private readonly HistoryStore? _history;
    private readonly Action<string> _open;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMinutes(1) };

    private readonly TextBlock _banglaDate = new();
    private readonly TextBlock _englishDate = new();
    private readonly TextBlock _hijriDate = new();
    private readonly Grid _prayerGrid = new() { ColumnSpacing = 8 };
    private readonly TextBlock _nextPrayer = new();
    private readonly ComboBox _districts = new() { MinWidth = 180 };
    private bool _loadingDistricts;

    /// <param name="history">Null in private windows: nothing is remembered there, so only default sites show.</param>
    /// <param name="open">Opens an address (replacing this new tab).</param>
    internal NewTabView(HistoryStore? history, Action<string> open)
    {
        _history = history;
        _open = open;
        InitializeComponent();

        Page.Children.Add(BuildDates());
        Page.Children.Add(BuildPrayerCard());
        Page.Children.Add(BuildSpeedDial());

        Loaded += (_, _) =>
        {
            Refresh();
            _timer.Tick += Timer_Tick;
            _timer.Start();
        };
        Unloaded += (_, _) =>
        {
            _timer.Stop();
            _timer.Tick -= Timer_Tick;
        };
    }

    private void Timer_Tick(object? sender, object e) => Refresh();

    /// <summary>The clock and date of Bangladesh (UTC+6), which the prayer times are for.</summary>
    private static DateTime BangladeshNow() => DateTime.UtcNow.AddHours(Districts.UtcOffsetHours);

    // ---- Dates ----

    private UIElement BuildDates()
    {
        var grid = new Grid { ColumnSpacing = 12 };
        for (var i = 0; i < 3; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        AddDateCard(grid, 0, "NewTabDateBangla", _banglaDate);
        AddDateCard(grid, 1, "NewTabDateEnglish", _englishDate);
        AddDateCard(grid, 2, "NewTabDateHijri", _hijriDate);

        // The Hijri card is Saudi Arabia's calendar, which Bangladesh does not follow.
        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(grid);
        stack.Children.Add(new TextBlock
        {
            Text = Strings.Get("NewTabHijriNote"),
            TextWrapping = TextWrapping.Wrap,
            Style = AppStyle("SecondaryCaptionTextBlockStyle"),
        });
        return stack;
    }

    private static void AddDateCard(Grid grid, int column, string labelKey, TextBlock value)
    {
        value.TextWrapping = TextWrapping.Wrap;
        value.Style = AppStyle("BodyStrongTextBlockStyle");
        var stack = new StackPanel { Spacing = 4 };
        stack.Children.Add(new TextBlock { Text = Strings.Get(labelKey), Style = AppStyle("SecondaryCaptionTextBlockStyle") });
        stack.Children.Add(value);

        var card = new Border { Child = stack, Style = AppStyle("SettingsCardStyle") };
        Grid.SetColumn(card, column);
        grid.Children.Add(card);
    }

    private void ShowDates(DateOnly today)
    {
        var bangla = BanglaCalendar.FromGregorian(today);
        var banglaText = Strings.Format("NewTabBanglaDateFormat",
            Formatting.Number(bangla.Day), Strings.Get("BanglaMonth" + N(bangla.Month)), Formatting.Digits(N(bangla.Year)));
        _banglaDate.Text = Strings.Format("NewTabBanglaDateSeasonFormat", banglaText, Strings.Get("BanglaSeason" + N(bangla.Season)));

        _englishDate.Text = Strings.Format("NewTabGregorianDateFormat",
            Strings.Get("Weekday" + N((int)today.DayOfWeek)),
            Formatting.Number(today.Day),
            Strings.Get("Month" + N(today.Month)),
            Formatting.Digits(N(today.Year)));

        var hijri = HijriCalendar.FromGregorian(today, AppServices.Settings.HijriAdjustment);
        _hijriDate.Text = Strings.Format("NewTabHijriDateFormat",
            Formatting.Number(hijri.Day), Strings.Get("HijriMonth" + N(hijri.Month)), Formatting.Digits(N(hijri.Year)));
    }

    // ---- Prayer times ----

    private UIElement BuildPrayerCard()
    {
        var stack = new StackPanel { Spacing = 12 };

        var header = new Grid { ColumnSpacing = 12 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var title = new TextBlock
        {
            Text = Strings.Get("NewTabPrayerTitle"),
            Style = AppStyle("SubtitleTextBlockStyle"),
            VerticalAlignment = VerticalAlignment.Center,
        };
        header.Children.Add(title);

        _loadingDistricts = true;
        foreach (var district in Districts.All)
        {
            _districts.Items.Add(AppServices.Settings.IsBangla ? district.Bn : district.En);
        }
        _districts.SelectedIndex = IndexOfDistrict(AppServices.Settings.PrayerDistrict);
        _loadingDistricts = false;
        _districts.Header = Strings.Get("NewTabPrayerDistrict");
        _districts.SelectionChanged += (_, _) =>
        {
            if (!_loadingDistricts && _districts.SelectedIndex >= 0)
            {
                AppServices.Settings.PrayerDistrict = Districts.All[_districts.SelectedIndex].Id;
                ShowPrayerTimes(BangladeshNow());
            }
        };
        Grid.SetColumn(_districts, 1);
        header.Children.Add(_districts);
        stack.Children.Add(header);

        for (var i = 0; i < 6; i++)
        {
            _prayerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }
        stack.Children.Add(_prayerGrid);

        _nextPrayer.Style = AppStyle("BodyStrongTextBlockStyle");
        stack.Children.Add(_nextPrayer);
        stack.Children.Add(new TextBlock
        {
            Text = Strings.Get("NewTabPrayerNote"),
            TextWrapping = TextWrapping.Wrap,
            Style = AppStyle("SecondaryCaptionTextBlockStyle"),
        });

        return new Border { Child = stack, Style = AppStyle("SettingsCardStyle") };
    }

    private static int IndexOfDistrict(string id)
    {
        for (var i = 0; i < Districts.All.Count; i++)
        {
            if (Districts.All[i].Id == id)
            {
                return i;
            }
        }
        return 0;
    }

    private void ShowPrayerTimes(DateTime now)
    {
        var district = Districts.Get(AppServices.Settings.PrayerDistrict);
        var today = DateOnly.FromDateTime(now);
        var times = PrayerCalculator.Calculate(today, district.Lat, district.Lon, Districts.UtcOffsetHours);

        var clock = TimeOnly.FromDateTime(now);
        PrayerKind nextKind = PrayerKind.Fajr;
        TimeOnly nextTime = default;
        var found = false;
        foreach (var (kind, time) in times.Prayers)
        {
            if (time > clock)
            {
                (nextKind, nextTime, found) = (kind, time, true);
                break;
            }
        }
        if (!found)
        {
            // Past Isha: the next prayer is tomorrow's Fajr.
            var tomorrow = PrayerCalculator.Calculate(today.AddDays(1), district.Lat, district.Lon, Districts.UtcOffsetHours);
            (nextKind, nextTime) = (PrayerKind.Fajr, tomorrow.Fajr);
        }

        _prayerGrid.Children.Clear();
        var cells = new (string NameKey, TimeOnly Time, PrayerKind? Kind)[]
        {
            ("PrayerFajr", times.Fajr, PrayerKind.Fajr),
            ("PrayerSunrise", times.Sunrise, null),
            ("PrayerDhuhr", times.Dhuhr, PrayerKind.Dhuhr),
            ("PrayerAsr", times.Asr, PrayerKind.Asr),
            ("PrayerMaghrib", times.Maghrib, PrayerKind.Maghrib),
            ("PrayerIsha", times.Isha, PrayerKind.Isha),
        };
        for (var i = 0; i < cells.Length; i++)
        {
            var isNext = cells[i].Kind == nextKind;
            var name = new TextBlock { Text = Strings.Get(cells[i].NameKey), Style = AppStyle("CaptionTextBlockStyle") };
            var time = new TextBlock { Text = Formatting.Time(cells[i].Time), Style = AppStyle("BodyStrongTextBlockStyle"), TextWrapping = TextWrapping.Wrap };
            if (isNext)
            {
                name.Style = AppStyle("OnAccentCaptionStyle");
                time.Style = AppStyle("OnAccentBodyStrongStyle");
            }

            var cell = new StackPanel { Spacing = 2 };
            cell.Children.Add(name);
            cell.Children.Add(time);
            var border = new Border { Child = cell, Style = AppStyle(isNext ? "NextPrayerCellStyle" : "PrayerCellStyle") };
            Grid.SetColumn(border, i);
            _prayerGrid.Children.Add(border);
        }

        _nextPrayer.Text = Strings.Format("NewTabPrayerNextFormat", Strings.Get("Prayer" + nextKind), Formatting.Time(nextTime));
    }

    // ---- Speed dial ----

    private UIElement BuildSpeedDial()
    {
        var stack = new StackPanel { Spacing = 12 };
        stack.Children.Add(new TextBlock { Text = Strings.Get("NewTabTopSites"), Style = AppStyle("SubtitleTextBlockStyle") });

        var tiles = new List<(string Label, string Url)>();
        if (_history is not null)
        {
            tiles.AddRange(_history.TopSites(TileCount).Select(site => (site.Host, site.Url)));
        }
        foreach (var (labelKey, url) in DefaultSites)
        {
            if (tiles.Count >= TileCount)
            {
                break;
            }
            var host = new Uri(url).Host.Replace("www.", string.Empty, StringComparison.Ordinal);
            if (!tiles.Any(t => new Uri(t.Url).Host.Replace("www.", string.Empty, StringComparison.Ordinal).Equals(host, StringComparison.OrdinalIgnoreCase)))
            {
                tiles.Add((Strings.Get(labelKey), url));
            }
        }

        var panel = new ItemsRepeater
        {
            ItemsSource = tiles.Select(t => BuildTile(t.Label, t.Url)).ToList(),
            Layout = new UniformGridLayout
            {
                MinItemWidth = TileWidth,
                MinItemHeight = 104,
                MinRowSpacing = 8,
                MinColumnSpacing = 8,
                ItemsStretch = UniformGridLayoutItemsStretch.None,
            },
        };
        stack.Children.Add(panel);
        return stack;
    }

    private Button BuildTile(string label, string url)
    {
        var badge = new Border { Style = AppStyle("MonogramBadgeStyle"), HorizontalAlignment = HorizontalAlignment.Center };
        badge.Child = new TextBlock
        {
            Text = label.Length > 0 ? char.ToUpper(label[0], System.Globalization.CultureInfo.CurrentCulture).ToString() : string.Empty,
            Style = AppStyle("OnAccentBodyStrongStyle"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var content = new StackPanel { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center };
        content.Children.Add(badge);
        content.Children.Add(new TextBlock
        {
            Text = label,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextAlignment = TextAlignment.Center,
            MaxWidth = TileWidth - 16,
        });

        var tile = new Button
        {
            Content = content,
            Width = TileWidth,
            Height = 104,
            Tag = url,
        };
        ToolTipService.SetToolTip(tile, url);
        tile.Click += (_, _) => _open(url);
        return tile;
    }

    // ---- Refresh ----

    private void Refresh()
    {
        var now = BangladeshNow();
        ShowDates(DateOnly.FromDateTime(now));
        ShowPrayerTimes(now);
    }

    private static string N(int value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static Style AppStyle(string key) => (Style)Application.Current.Resources[key];
}
