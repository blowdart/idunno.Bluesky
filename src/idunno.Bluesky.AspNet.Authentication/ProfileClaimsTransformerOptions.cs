// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.AspNet.Authentication;

/// <summary>
/// Contains options for the <see cref="ProfileClaimsTransformer"/>.
/// </summary>
public record ProfileClaimsTransformerOptions
{
    /// <summary>
    /// The profile cache used to cache profile information during claims transformation. If not provided a default in-memory store will be used.
    /// </summary>
    public IProfileCache? Cache { get; set; } = default!;

    /// <summary>
    /// Gets or sets a <see cref="TimeSpan"/> indicating how long a profile entry will last for in the cache. Defaults to 15 minutes.
    /// </summary>
    public TimeSpan CacheTimeout { get; set; } = new TimeSpan(0, 15, 0);
}
