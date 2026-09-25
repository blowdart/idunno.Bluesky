// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.Claims;

using idunno.AtProto;

namespace idunno.Bluesky.AspNet.Authentication.Test;

public class ClaimsExtensionsTests
{
    private static ClaimsPrincipal PrincipalWith(string claimType, string value) =>
        new(new ClaimsIdentity([new Claim(claimType, value)], "Bluesky"));

    [Fact]
    public void ProfileClaimsAreSurfacedFromThePrincipal()
    {
        ClaimsPrincipal principal = new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.DisplayName, "A Display Name"),
                new Claim(ClaimTypes.Handle, "example.bsky.social"),
                new Claim(ClaimTypes.Description, "A description"),
                new Claim(ClaimTypes.Pronouns, "they/them"),
                new Claim(ClaimTypes.Website, "https://example.org/"),
                new Claim(ClaimTypes.Avatar, "https://cdn.example.org/avatar.jpg"),
                new Claim(ClaimTypes.Banner, "https://cdn.example.org/banner.jpg"),
            ],
            "Bluesky"));

        Assert.Equal("A Display Name", principal.GetDisplayName());
        Assert.Equal(new Handle("example.bsky.social"), principal.GetHandle());
        Assert.Equal("A description", principal.GetDescription());
        Assert.Equal("they/them", principal.GetPronouns());
        Assert.Equal(new Uri("https://example.org/"), principal.GetWebsite());
        Assert.Equal(new Uri("https://cdn.example.org/avatar.jpg"), principal.GetAvatar());
        Assert.Equal(new Uri("https://cdn.example.org/banner.jpg"), principal.GetBanner());
    }

    [Fact]
    public void ProfileClaimsAreNullWhenThePrincipalDoesNotCarryThem()
    {
        ClaimsPrincipal principal = new(new ClaimsIdentity([], "Bluesky"));

        Assert.Null(principal.GetDisplayName());
        Assert.Null(principal.GetHandle());
        Assert.Null(principal.GetDescription());
        Assert.Null(principal.GetPronouns());
        Assert.Null(principal.GetWebsite());
        Assert.Null(principal.GetAvatar());
        Assert.Null(principal.GetBanner());
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("JAVASCRIPT:alert(1)")]
    [InlineData("data:text/html;base64,PHNjcmlwdD5hbGVydCgxKTwvc2NyaXB0Pg==")]
    [InlineData("file:///etc/passwd")]
    [InlineData("vbscript:msgbox(1)")]
    [InlineData("ftp://example.org/file")]
    [InlineData("not a uri at all")]
    [InlineData("/relative/path")]
    public void UriProfileClaimsWhichAreNotHttpOrHttpsAreNotSurfaced(string value)
    {
        // These come from user supplied profile fields, and an application rendering one as an anchor target would be
        // handing out a script injection vector.
        Assert.Null(PrincipalWith(ClaimTypes.Website, value).GetWebsite());
        Assert.Null(PrincipalWith(ClaimTypes.Avatar, value).GetAvatar());
        Assert.Null(PrincipalWith(ClaimTypes.Banner, value).GetBanner());
    }

    [Theory]
    [InlineData("http://example.org/")]
    [InlineData("https://example.org/")]
    public void HttpAndHttpsProfileClaimsAreSurfaced(string value)
    {
        Assert.Equal(new Uri(value), PrincipalWith(ClaimTypes.Website, value).GetWebsite());
        Assert.Equal(new Uri(value), PrincipalWith(ClaimTypes.Avatar, value).GetAvatar());
        Assert.Equal(new Uri(value), PrincipalWith(ClaimTypes.Banner, value).GetBanner());
    }

    [Fact]
    public void AHandleClaimWhichIsNotAValidHandleIsNotSurfaced()
    {
        Assert.Null(PrincipalWith(ClaimTypes.Handle, "not a handle").GetHandle());
    }

    [Fact]
    public void ProfileClaimsAreNullForANullPrincipalRatherThanThrowing()
    {
        ClaimsPrincipal? principal = null;

        Assert.Null(principal.GetDisplayName());
        Assert.Null(principal.GetHandle());
        Assert.Null(principal.GetDescription());
        Assert.Null(principal.GetPronouns());
        Assert.Null(principal.GetWebsite());
        Assert.Null(principal.GetAvatar());
        Assert.Null(principal.GetBanner());
    }
}
