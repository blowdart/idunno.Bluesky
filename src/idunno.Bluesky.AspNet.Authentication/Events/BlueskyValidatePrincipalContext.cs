// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace idunno.Bluesky.AspNet.Authentication.Events;

/// <summary>
/// Context object passed to the <see cref="BlueskyAuthenticationEvents.ValidatePrincipal(BlueskyValidatePrincipalContext)"/> method.
/// </summary>
public class BlueskyValidatePrincipalContext : PrincipalContext<BlueskyAuthenticationOptions>
{
    /// <summary>
    /// Creates a new instance of <see cref="BlueskyValidatePrincipalContext"/>
    /// </summary>
    /// <param name="context">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="scheme">The authentication scheme.</param>
    /// <param name="options">The <see cref="BlueskyAuthenticationOptions"/>.</param>
    /// <param name="ticket">The <see cref="AuthenticationTicket"/> for the current request.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="ticket"/> is <see langword="null" />.</exception>
    public BlueskyValidatePrincipalContext(HttpContext context, AuthenticationScheme scheme, BlueskyAuthenticationOptions options, AuthenticationTicket ticket)
        : base(context, scheme, options, ticket?.Properties)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        Principal = ticket.Principal;
    }

    /// <summary>
    /// If <see langword="true" />, the cookie will be renewed
    /// </summary>
    public bool ShouldRenew { get; set; }

    /// <summary>
    /// Called to replace the claims principal. The supplied principal will replace the value of the
    /// <see cref="PrincipalContext{TOptions}.Principal"/> property, which determines the identity of the authenticated request.
    /// </summary>
    /// <param name="principal">The <see cref="ClaimsPrincipal"/> used as the replacement</param>
    public void ReplacePrincipal(ClaimsPrincipal principal) => Principal = principal;

    /// <summary>
    /// Called to reject the incoming principal. This may be done if the application has determined the
    /// account is no longer active, and the request should be treated as if it was anonymous.
    /// </summary>
    public void RejectPrincipal() => Principal = null;
}
