// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace idunno.Bluesky.AspNet.Authentication.Events;

/// <summary>
/// Context object passed to the <see cref="BlueskyAuthenticationEvents.OnCheckSlidingExpiration"/> method.
/// </summary>
public class BlueskySlidingExpirationContext : PrincipalContext<BlueskyAuthenticationOptions>
{
    /// <summary>
    /// Creates a new instance of <see cref="BlueskySlidingExpirationContext"/>
    /// </summary>
    /// <param name="context">The HTTP request context</param>
    /// <param name="scheme">The scheme data</param>
    /// <param name="options">The handler options</param>
    /// <param name="ticket">The authentication ticket</param>
    /// <param name="elapsedTime">The amount of time that has elapsed since the cookie was issued or renewed</param>
    /// <param name="remainingTime">The amount of time left until the cookie expires</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="ticket"/> is <see langword="null"/>.</exception>
    public BlueskySlidingExpirationContext(HttpContext context, AuthenticationScheme scheme, BlueskyAuthenticationOptions options,
        AuthenticationTicket ticket, TimeSpan elapsedTime, TimeSpan remainingTime)
        : base(context, scheme, options, ticket?.Properties)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        Principal = ticket.Principal;
        ElapsedTime = elapsedTime;
        RemainingTime = remainingTime;
    }

    /// <summary>
    /// The amount of time that has elapsed since the cookie was issued or renewed.
    /// </summary>
    public TimeSpan ElapsedTime { get; }

    /// <summary>
    /// The amount of time left until the cookie expires.
    /// </summary>
    public TimeSpan RemainingTime { get; }

    /// <summary>
    /// If <see langword="true" />, the cookie will be renewed. The initial value will be <see langword="true" /> if the elapsed time
    /// is greater than the remaining time (e.g. more than 50% expired).
    /// </summary>
    public bool ShouldRenew { get; set; }
}
