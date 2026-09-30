using System.Diagnostics;
using System.Reflection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Obhijatri.App.Localization;
using Obhijatri.App.Services;

namespace Obhijatri.App.Views;

/// <summary>
/// The About page: name and version, what leaves the computer (and what never does), the browser
/// engine's version, where the data lives, and the open source notices, with the source of the MPL
/// licensed files in the Legal folder beside the app.
/// </summary>
public sealed partial class AboutView : UserControl
{
    private readonly TextBlock _engine = new() { TextWrapping = TextWrapping.Wrap };

    internal AboutView()
    {
        InitializeComponent();

        Page.Children.Add(BuildHeader());
        Page.Children.Add(Card("AboutPrivacyTitle", Wrapped(Strings.Get("AboutPrivacyBody"))));
        Page.Children.Add(Card("AboutEngineTitle", _engine));
        Page.Children.Add(Card("AboutDataTitle", BuildData()));
        Page.Children.Add(Card("AboutLicensesTitle", BuildLicenses()));

        _engine.Text = Strings.Get("AboutEngineLooking");
        Loaded += async (_, _) => await ShowEngineVersionAsync();
    }

    private UIElement BuildHeader()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 20 };
        var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Icons", "Square150x150Logo.png");
        if (File.Exists(iconPath))
        {
            row.Children.Add(new Image { Source = new BitmapImage(new Uri(iconPath)), Width = 96, Height = 96 });
        }

        var version = typeof(App).Assembly.GetName().Version ?? new Version(0, 0, 0);
        var text = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock { Text = Strings.Get("AppTitle"), Style = AppStyle("TitleLargeTextBlockStyle") });
        text.Children.Add(new TextBlock
        {
            Text = Strings.Format("AboutVersionFormat", Formatting.Digits($"{version.Major}.{version.Minor}.{version.Build}")),
            Style = AppStyle("SecondaryCaptionTextBlockStyle"),
        });
        text.Children.Add(Wrapped(Strings.Get("AboutTagline")));
        row.Children.Add(text);
        return row;
    }

    private static UIElement BuildData()
    {
        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(Wrapped(Strings.Get("AboutDataBody")));
        stack.Children.Add(new TextBlock { Text = DataFolder.Path, IsTextSelectionEnabled = true, TextWrapping = TextWrapping.Wrap, Style = AppStyle("CaptionTextBlockStyle") });
        var open = new Button { Content = Strings.Get("AboutOpenFolder") };
        open.Click += (_, _) => DataFolder.Open();
        stack.Children.Add(open);
        return stack;
    }

    private static UIElement BuildLicenses()
    {
        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(Wrapped(Strings.Get("AboutLicensesBody")));

        var legal = System.IO.Path.Combine(AppContext.BaseDirectory, "Legal");
        var openLegal = new Button { Content = Strings.Get("AboutOpenLegal"), IsEnabled = Directory.Exists(legal) };
        openLegal.Click += (_, _) => Process.Start(new ProcessStartInfo("explorer.exe") { ArgumentList = { legal } });
        stack.Children.Add(openLegal);

        var notices = new TextBlock
        {
            Text = ReadNotices(),
            IsTextSelectionEnabled = true,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"), // not-ui
            FontSize = 12,
        };
        stack.Children.Add(new Expander
        {
            Header = Strings.Get("AboutNoticesExpander"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Content = notices,
        });
        return stack;
    }

    /// <summary>The third party notices file, embedded in the app so the About page never depends on a file being present.</summary>
    private static string ReadNotices()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Obhijatri.App.ThirdPartyNotices.md");
        if (stream is null)
        {
            return string.Empty;
        }
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private async Task ShowEngineVersionAsync()
    {
        try
        {
            var environment = await WebViewEnvironment.GetAsync();
            _engine.Text = Strings.Format("AboutEngineFormat", Formatting.Digits(environment.BrowserVersionString));
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException or ArgumentException)
        {
            _engine.Text = Strings.Get("AboutEngineUnknown");
        }
    }

    private static Border Card(string titleKey, UIElement content)
    {
        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(new TextBlock { Text = Strings.Get(titleKey), Style = AppStyle("SubtitleTextBlockStyle") });
        stack.Children.Add(content);
        return new Border { Child = stack, Style = AppStyle("SettingsCardStyle") };
    }

    private static TextBlock Wrapped(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap };

    private static Style AppStyle(string key) => (Style)Application.Current.Resources[key];
}
