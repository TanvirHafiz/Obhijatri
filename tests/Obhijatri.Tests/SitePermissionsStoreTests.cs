using Obhijatri.Core.Storage;

namespace Obhijatri.Tests;

public sealed class SitePermissionsStoreTests : IDisposable
{
    private readonly BrowserDatabase _db = BrowserDatabase.OpenInMemory();
    private readonly SitePermissionsStore _store;

    public SitePermissionsStoreTests() => _store = new SitePermissionsStore(_db);

    public void Dispose() => _db.Dispose();

    [Fact]
    public void DefaultIsAsk()
    {
        Assert.Equal(SitePermissionState.Ask, _store.Get("example.com", SitePermissionKind.Camera));
        Assert.Empty(_store.ListSitesWithDecisions());
    }

    [Fact]
    public void SetAndGet_RoundTrips_PerKind()
    {
        _store.Set("example.com", SitePermissionKind.Camera, SitePermissionState.Allow);
        _store.Set("example.com", SitePermissionKind.Microphone, SitePermissionState.Deny);

        Assert.Equal(SitePermissionState.Allow, _store.Get("example.com", SitePermissionKind.Camera));
        Assert.Equal(SitePermissionState.Deny, _store.Get("example.com", SitePermissionKind.Microphone));
        Assert.Equal(SitePermissionState.Ask, _store.Get("example.com", SitePermissionKind.Location));

        var rows = _store.ListSitesWithDecisions();
        var row = Assert.Single(rows);
        Assert.Equal("example.com", row.Host);
        Assert.True(row.HasAnyDecision);
    }

    [Fact]
    public void SettingBackToAsk_RemovesTheRow_OnceEverythingIsDefault()
    {
        _store.Set("example.com", SitePermissionKind.Notifications, SitePermissionState.Deny);
        Assert.Single(_store.ListSitesWithDecisions());

        _store.Set("example.com", SitePermissionKind.Notifications, SitePermissionState.Ask);
        Assert.Empty(_store.ListSitesWithDecisions());
    }

    [Fact]
    public void HostIsNormalized()
    {
        _store.Set("Example.COM.", SitePermissionKind.Location, SitePermissionState.Allow);
        Assert.Equal(SitePermissionState.Allow, _store.Get("example.com", SitePermissionKind.Location));
    }
}
