// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Authentication;

namespace idunno.AtProto.Test;

[ExcludeFromCodeCoverage]
public class OpenBrowserTests
{
    // Only the rejection cases are exercised, as an accepted uri would launch the platform browser.

    [Fact]
    public void OpenBrowserThrowsOnANullUri()
    {
        Assert.Throws<ArgumentNullException>("uri", () => OAuthClient.OpenBrowser(null!));
    }

    [Fact]
    public void OpenBrowserThrowsOnARelativeUri()
    {
        Assert.Throws<ArgumentException>("uri", () => OAuthClient.OpenBrowser(new Uri("/authorize", UriKind.Relative)));
    }

    [Theory]
    [InlineData("file:///c:/windows/system32/calc.exe")]
    [InlineData("ms-msdt:/id PCWDiagnostic")]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://example.test/")]
    [InlineData("search-ms:query=x")]
    public void OpenBrowserThrowsOnANonHttpScheme(string uri)
    {
        Assert.Throws<ArgumentException>("uri", () => OAuthClient.OpenBrowser(new Uri(uri, UriKind.Absolute)));
    }
}
