// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.Claims;

using idunno.AtProto;

namespace idunno.Bluesky.AspNet.Authentication;

/// <summary>
/// Adds some utilities to the <see cref="ClaimsPrincipal"/> and <see cref="ClaimsIdentity"/> classes.
/// </summary>
public static class ClaimsExtensions
{
    /// <summary>
    /// Gets a value indicating whether <paramref name="uri"/> is safe to surface to an application as a web link.
    /// </summary>
    /// <param name="uri">The <see cref="Uri"/> to check.</param>
    /// <returns><see langword="true"/> if <paramref name="uri"/> is an HTTP or HTTPS URI; otherwise <see langword="false"/>.</returns>
    /// <remarks>
    /// <para>
    ///   Profile URIs originate from user supplied profile fields, and <see cref="Uri.TryCreate(string?, UriKind, out Uri?)"/>
    ///   happily parses schemes such as <c>javascript:</c>. Returning one of those would hand an application a script
    ///   injection vector the moment it rendered the value as an anchor target, so only HTTP and HTTPS are allowed.
    /// </para>
    /// </remarks>
    internal static bool IsSafeWebUri(Uri? uri) =>
        uri is not null &&
        (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.Ordinal) ||
         uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.Ordinal));

    /// <summary>
    /// Gets the value of the first claim of type <paramref name="claimType"/> from <paramref name="principal"/>, if any.
    /// </summary>
    /// <param name="principal">The <see cref="ClaimsPrincipal"/> to read the claim from.</param>
    /// <param name="claimType">The type of the claim to read.</param>
    /// <returns>The claim value if present; otherwise, <see langword="null"/>.</returns>
    private static string? GetClaimValue(ClaimsPrincipal? principal, string claimType)
    {
        Claim? claim = principal?.Claims.FirstOrDefault(c => c.Type == claimType);

        return claim?.Value;
    }

    /// <summary>
    /// Gets the value of the first claim of type <paramref name="claimType"/> from <paramref name="principal"/>,
    /// if any, and it converts to a valid HTTP or HTTPS <see cref="Uri"/>.
    /// </summary>
    /// <param name="principal">The <see cref="ClaimsPrincipal"/> to read the claim from.</param>
    /// <param name="claimType">The type of the claim to read.</param>
    /// <returns>The claim value as a <see cref="Uri"/> if present and safe; otherwise, <see langword="null"/>.</returns>
    private static Uri? GetSafeWebUriClaimValue(ClaimsPrincipal? principal, string claimType)
    {
        if (GetClaimValue(principal, claimType) is string value &&
            Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) &&
            IsSafeWebUri(uri))
        {
            return uri;
        }

        return null;
    }

    /// <summary>
    /// Gets the Bluesky display name from the specified <paramref name="principal"/>, if any.
    /// </summary>
    /// <param name="principal">The <see cref="ClaimsPrincipal"/> to get the display name from.</param>
    /// <returns>The display name if present; otherwise, <see langword="null"/>.</returns>
    public static string? GetDisplayName(this ClaimsPrincipal? principal) =>
        GetClaimValue(principal, Bluesky.ClaimTypes.DisplayName);

    /// <summary>
    /// Gets the Bluesky <see cref="AtProto.Handle"/> from the specified <paramref name="principal"/>, if any.
    /// </summary>
    /// <param name="principal">The <see cref="ClaimsPrincipal"/> to get the handle from.</param>
    /// <returns>The <see cref="AtProto.Handle"/> if present and valid; otherwise, <see langword="null"/>.</returns>
    public static Handle? GetHandle(this ClaimsPrincipal? principal)
    {
        if (GetClaimValue(principal, Bluesky.ClaimTypes.Handle) is string value &&
            Handle.TryParse(value, out Handle? handle))
        {
            return handle;
        }

        return null;
    }

    /// <summary>
    /// Gets the user's self description from the specified <paramref name="principal"/>, if any.
    /// </summary>
    /// <param name="principal">The <see cref="ClaimsPrincipal"/> to get the description from.</param>
    /// <returns>The description if present; otherwise, <see langword="null"/>.</returns>
    public static string? GetDescription(this ClaimsPrincipal? principal) =>
        GetClaimValue(principal, Bluesky.ClaimTypes.Description);

    /// <summary>
    /// Gets the user's specified pronouns from the specified <paramref name="principal"/>, if any.
    /// </summary>
    /// <param name="principal">The <see cref="ClaimsPrincipal"/> to get the pronouns from.</param>
    /// <returns>The pronouns if present; otherwise, <see langword="null"/>.</returns>
    public static string? GetPronouns(this ClaimsPrincipal? principal) =>
        GetClaimValue(principal, Bluesky.ClaimTypes.Pronouns);

    /// <summary>
    /// Gets the user's specified website from the specified <paramref name="principal"/>, if any and it converts to a valid HTTP or HTTPS <see cref="Uri"/>.
    /// </summary>
    /// <param name="principal">The <see cref="ClaimsPrincipal"/> to get the website from.</param>
    /// <returns>The website if present; otherwise, <see langword="null"/>.</returns>
    public static Uri? GetWebsite(this ClaimsPrincipal? principal) =>
        GetSafeWebUriClaimValue(principal, Bluesky.ClaimTypes.Website);

    /// <summary>
    /// Gets the URI for the user's specified avatar from the specified <paramref name="principal"/>, if any and it converts to a valid HTTP or HTTPS <see cref="Uri"/>.
    /// </summary>
    /// <param name="principal">The <see cref="ClaimsPrincipal"/> to get the avatar from.</param>
    /// <returns>The avatar if present; otherwise, <see langword="null"/>.</returns>
    public static Uri? GetAvatar(this ClaimsPrincipal? principal) =>
        GetSafeWebUriClaimValue(principal, Bluesky.ClaimTypes.Avatar);

    /// <summary>
    /// Gets the URI for the user's specified profile banner from the specified <paramref name="principal"/>, if any and it converts to a valid HTTP or HTTPS <see cref="Uri"/>.
    /// </summary>
    /// <param name="principal">The <see cref="ClaimsPrincipal"/> to get the banner from.</param>
    /// <returns>The banner if present; otherwise, <see langword="null"/>.</returns>
    public static Uri? GetBanner(this ClaimsPrincipal? principal) =>
        GetSafeWebUriClaimValue(principal, Bluesky.ClaimTypes.Banner);
}
