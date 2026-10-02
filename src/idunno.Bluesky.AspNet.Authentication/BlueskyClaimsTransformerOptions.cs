// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.AspNet.Authentication;

/// <summary>
/// Contains options for the <see cref="BlueskyClaimsTransformer"/>.
/// </summary>
public record BlueskyClaimsTransformerOptions
{
    /// <summary>
    /// Gets or sets the provider used to check whether a principal's credentials have expired, and by the default profile cache to expire entries.
    /// </summary>
    /// <value>The time provider. The default is <see cref="TimeProvider.System"/>.</value>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    ///   The provider is always used for credential expiry checks, and is passed to the agent created to retrieve a profile.
    /// </para>
    /// <para>
    ///   It controls profile expiry only when <see cref="Cache"/> is not set and the default in-memory cache is created.
    ///   A custom <see cref="IProfileCache"/> controls its own expiry and does not use this provider.
    /// </para>
    /// </remarks>
    public TimeProvider TimeProvider
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    } = TimeProvider.System;

    /// <summary>
    /// The profile cache used to cache profile information during claims transformation. If not provided a default in-memory store will be used.
    /// </summary>
        /// <remarks>
        /// <para>
        ///   The default in-memory store expires entries after <see cref="CacheTimeout"/>, using <see cref="TimeProvider"/>.
        ///   A custom cache controls its own expiry.
        /// </para>
        /// </remarks>
    public IProfileCache? Cache { get; set; } = default!;

    /// <summary>
    /// Gets or sets a <see cref="TimeSpan"/> indicating how long a profile entry will last for in the cache. Defaults to 15 minutes.
    /// </summary>
    public TimeSpan CacheTimeout { get; set; } = new TimeSpan(0, 15, 0);

    /// <summary>
    /// Gets or sets a flag indicating whether a handle should be verified against the directory before it becomes a claim. Defaults to <see langword="true" />.
    /// </summary>
    /// <remarks>
    /// <para>
    ///   The handle a profile carries is asserted by the user's own personal data server, which can put any handle it
    ///   likes there, so it proves nothing until the directory and the handle owner agree with it. A handle which does
    ///   not verify is dropped, and neither the handle claim nor the name claim is added for it.
    /// </para>
    /// <para>
    ///   Verification costs a directory lookup and a handle resolution on each profile cache miss. Turn it off only
    ///   where the handle is never used to name or to authorize a user.
    /// </para>
    /// </remarks>
    public bool VerifyHandle { get; set; } = true;
}
