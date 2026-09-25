// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.Claims;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace idunno.Bluesky.AspNet.Authentication.Events;

/// <summary>
/// Context object passed to the <see cref="BlueskyAuthenticationEvents.SigningIn"/> method.
/// </summary>
public class BlueskySigningInContext : PrincipalContext<BlueskyAuthenticationOptions>
{
    /// <summary>
    /// Creates a new instance of the context object.
    /// </summary>
    /// <param name="context">The HTTP request context</param>
    /// <param name="scheme">The scheme data</param>
    /// <param name="options">The handler options</param>
    /// <param name="principal">Initializes Principal property</param>
    /// <param name="properties">The authentication properties.</param>
    /// <param name="cookieOptions">Initializes options for the authentication cookie.</param>
    public BlueskySigningInContext(
        HttpContext context,
        AuthenticationScheme scheme,
        BlueskyAuthenticationOptions options,
        ClaimsPrincipal principal,
        AuthenticationProperties? properties,
        CookieOptions cookieOptions)
        : base(context, scheme, options, properties)
    {
        CookieOptions = cookieOptions;
        Principal = principal;
    }

    /// <summary>
    /// The options for creating the outgoing cookie.
    /// May be replace or altered during the SigningIn call.
    /// </summary>
    public CookieOptions CookieOptions { get; set; }
}
