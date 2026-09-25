using Obhijatri.Core;

namespace Obhijatri.Tests;

public sealed class AddressResolverTests
{
    [Theory]
    [InlineData("prothomalo.com", "https://prothomalo.com/")]
    [InlineData("https://www.prothomalo.com", "https://www.prothomalo.com/")]
    [InlineData("http://example.com/a?b=1", "http://example.com/a?b=1")]
    [InlineData("localhost:8080", "http://localhost:8080/")]
    [InlineData("192.168.0.1", "https://192.168.0.1/")]
    [InlineData("example.com:8443/x", "https://example.com:8443/x")]
    [InlineData("xn--fcebook-8va.com", "https://xn--fcebook-8va.com/")]
    [InlineData("about:blank", "about:blank")]
    public void WebAddresses_OpenDirectly(string input, string expected)
    {
        Assert.Equal(expected, AddressResolver.Resolve(input)!.AbsoluteUri);
    }

    [Theory]
    [InlineData("বাংলাদেশ")]
    [InlineData("hello world")]
    [InlineData("1234")]
    [InlineData("3.14")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<b>x</b>")]
    [InlineData("file:///C:/Windows/win.ini")]
    [InlineData("vbscript:msgbox")]
    [InlineData("ms-settings:")]
    public void EverythingElse_IsASearch(string input)
    {
        Assert.StartsWith("https://www.google.com/search?q=", AddressResolver.Resolve(input)!.AbsoluteUri);
    }

    [Fact]
    public void Blank_IsNull() => Assert.Null(AddressResolver.Resolve("   "));
}

public sealed class AddressResolverAddressOnlyTests
{
    [Theory]
    [InlineData("prothomalo.com", true)]
    [InlineData("https://bdnews24.com", true)]
    [InlineData("hello world", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("বাংলাদেশ", false)]
    [InlineData("", false)]
    public void TryResolveAddress_OnlyAcceptsAddresses(string input, bool expected)
    {
        Assert.Equal(expected, AddressResolver.TryResolveAddress(input, out var uri));
        Assert.Equal(expected, uri is not null);
    }

    [Fact]
    public void Resolve_UsesTheGivenSearchEngine()
    {
        var uri = AddressResolver.Resolve("আমার সোনার বাংলা", "https://duckduckgo.com/?q={0}");
        Assert.StartsWith("https://duckduckgo.com/?q=", uri!.AbsoluteUri);
    }
}
