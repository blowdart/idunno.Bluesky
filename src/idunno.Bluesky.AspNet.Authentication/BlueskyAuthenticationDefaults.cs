// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Http;

namespace idunno.Bluesky.AspNet.Authentication;

/// <summary>
/// Contains default values related to Bluesky authentication.
/// </summary>
public static class BlueskyAuthenticationDefaults
{
    /// <summary>
    /// The default value used for the authentication scheme.
    /// </summary>
    public static readonly string AuthenticationScheme = "Bluesky";

    /// <summary>
    /// The default value used to display the authentication scheme.
    /// </summary>
    public static readonly string DisplayName = "Bluesky";

    /// <summary>
    /// The prefix used to provide a default CookieAuthenticationOptions.CookieName
    /// </summary>
    public static readonly string CookiePrefix = ".Bluesky.";

    /// <summary>
    /// The default value used by BlueskyAuthenticationMiddleware for the
    /// BlueskyAuthenticationDefaults.LoginPath
    /// </summary>
    public static readonly PathString LoginPath = new("/Account/Login");

    /// <summary>
    /// The default value used by BlueskyAuthenticationMiddleware for the
    /// BlueskyAuthenticationOptions.LogoutPath
    /// </summary>
    public static readonly PathString LogoutPath = new("/Account/Logout");

    /// <summary>
    /// The default value used by BlueskyAuthenticationMiddleware for the
    /// BlueskyAuthenticationOptions.AccessDeniedPath
    /// </summary>
    public static readonly PathString AccessDeniedPath = new("/Account/AccessDenied");

    /// <summary>
    /// The default value of the BlueskyAuthenticationOptions.ReturnUrlParameter
    /// </summary>
    public static readonly string ReturnUrlParameter = "ReturnUrl";
}
