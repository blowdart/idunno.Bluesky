// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace idunno.Bluesky.AspNet.Authentication.Events;

#pragma warning disable CSENSE020 // Potential ghost parameter reference in documentation
/// <summary>
/// Context object passed to the <see cref="BlueskyAuthenticationEvents.SigningOut"/> method.
/// </summary>
/// <param name="context">The <see cref="HttpContext"/> from the sign out request.</param>
/// <param name="scheme">The scheme name</param>
/// <param name="options">The authentication options</param>
/// <param name="properties">The authentication properties.</param>
/// <param name="cookieOptions">The options for the authentication cookies.</param>
public class BlueskySigningOutContext(
#pragma warning restore CSENSE020 // Potential ghost parameter reference in documentation
    HttpContext context,
    AuthenticationScheme scheme,
    BlueskyAuthenticationOptions options,
    AuthenticationProperties? properties,
    CookieOptions cookieOptions) : PropertiesContext<BlueskyAuthenticationOptions>(context, scheme, options, properties)
{
    /// <summary>
    /// The options for creating the outgoing cookie.
    /// May be replace or altered during the SigningOut call.
    /// </summary>
    public CookieOptions CookieOptions { get; set; } = cookieOptions;
}
