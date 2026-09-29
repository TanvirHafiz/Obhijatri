using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Obhijatri.App.Localization;
using Obhijatri.App.Services;
using Obhijatri.Core.Storage;

namespace Obhijatri.App;

/// <summary>
/// Camera, microphone, location and notification permissions (Milestone 7). Private windows keep
/// their own choices in memory only, like the other per-site settings in MainWindow.Shield.cs.
/// </summary>
public sealed partial class MainWindow
{
    private readonly Dictionary<(string Host, SitePermissionKind Kind), SitePermissionState> _privatePermissions = new();

    public SitePermissionState GetSitePermission(string host, SitePermissionKind kind) =>
        IsPrivate
            ? _privatePermissions.GetValueOrDefault((host, kind), SitePermissionState.Ask)
            : AppServices.SitePermissions.Get(host, kind);

    public void SetSitePermission(string host, SitePermissionKind kind, SitePermissionState state)
    {
        if (IsPrivate)
        {
            _privatePermissions[(host, kind)] = state;
        }
        else
        {
            AppServices.SitePermissions.Set(host, kind, state);
        }
    }

    public async void PromptForPermission(string host, SitePermissionKind kind, Action<bool> respond)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = Content.XamlRoot,
            Title = Strings.Format("PermissionPromptTitleFormat", host),
            Content = Strings.Get(PermissionPromptMessageKey(kind)),
            PrimaryButtonText = Strings.Get("PermissionAllow"),
            CloseButtonText = Strings.Get("PermissionDeny"),
            DefaultButton = ContentDialogButton.Close,
        };
        var result = await dialog.ShowAsync();
        respond(result == ContentDialogResult.Primary);
    }

    public void OnClipboardWrite(string host)
    {
        if (!AppServices.Settings.ClipboardGuardEnabled)
        {
            return;
        }
        ShowInfo(InfoBarSeverity.Warning, "ClipboardGuardTitle", Strings.Format("ClipboardGuardMessageFormat", host), messageIsKey: false);
    }

    private static string PermissionPromptMessageKey(SitePermissionKind kind) => kind switch
    {
        SitePermissionKind.Camera => "PermissionPromptCamera",
        SitePermissionKind.Microphone => "PermissionPromptMicrophone",
        SitePermissionKind.Location => "PermissionPromptLocation",
        _ => "PermissionPromptCamera",
    };

    private void UpdateNotificationChip()
    {
        var host = _activeTab?.PendingNotificationHost;
        NotificationChipButton.Visibility = host is null ? Visibility.Collapsed : Visibility.Visible;
        SetLabel(NotificationChipButton, "PermissionNotificationChipTooltip");
    }

    private void NotificationChipButton_Click(object sender, RoutedEventArgs e)
    {
        if (_activeTab?.PendingNotificationHost is not { } host)
        {
            return;
        }

        var panel = new StackPanel { Width = 280, Spacing = 12 };
        panel.Children.Add(new TextBlock
        {
            Text = Strings.Format("PermissionNotificationChipMessageFormat", host),
            TextWrapping = TextWrapping.Wrap,
        });
        var allow = new Button { Content = Strings.Get("PermissionAllow"), HorizontalAlignment = HorizontalAlignment.Stretch };
        allow.Click += (_, _) =>
        {
            SetSitePermission(host, SitePermissionKind.Notifications, SitePermissionState.Allow);
            _activeTab?.Reload();
        };
        panel.Children.Add(allow);
        panel.Children.Add(new TextBlock
        {
            Text = Strings.Get("PermissionNotificationChipHint"),
            TextWrapping = TextWrapping.Wrap,
            Style = (Style)Application.Current.Resources["SecondaryCaptionTextBlockStyle"],
        });

        new Flyout { Content = panel, Placement = Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.BottomEdgeAlignedRight }.ShowAt(NotificationChipButton);
    }
}
