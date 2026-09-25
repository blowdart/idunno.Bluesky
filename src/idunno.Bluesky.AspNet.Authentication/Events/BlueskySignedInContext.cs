// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.Claims;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace idunno.Bluesky.AspNet.Authentication.Events;

/// <summary>
/// Context object passed to the <see cref="BlueskyAuthenticationEvents.SignedIn"/> method.
/// </summary>
public class BlueskySignedInContext : PrincipalContext<BlueskyAuthenticationOptions>
{
    /// <summary>
    /// Creates a new instance of the context object.
    /// </summary>
    /// <param name="context">The HTTP request context</param>
    /// <param name="scheme">The scheme data</param>
    /// <param name="principal">Initializes Principal property</param>
    /// <param name="properties">Initializes Properties property</param>
    /// <param name="options">The handler options</param>
    public BlueskySignedInContext(
        HttpContext context,
        AuthenticationScheme scheme,
        ClaimsPrincipal principal,
        AuthenticationProperties? properties,
        BlueskyAuthenticationOptions options)
        : base(context, scheme, options, properties)
    {
        Principal = principal;
    }
}
