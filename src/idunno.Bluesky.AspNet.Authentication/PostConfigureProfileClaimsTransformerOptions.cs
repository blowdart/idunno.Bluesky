// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace idunno.Bluesky.AspNet.Authentication;

/// <summary>
/// Initializes a new instance of <see cref="PostConfigureProfileClaimsTransformerOptions"/> used to set default options..
/// </summary>
/// <param name="loggerFactory">The <see cref="ILoggerFactory"/> used to create loggers.</param>
public class PostConfigureProfileClaimsTransformerOptions(ILoggerFactory loggerFactory) : IPostConfigureOptions<ProfileClaimsTransformerOptions>
{
    /// <summary>
    /// Invoked to post configure a TOptions instance.
    /// </summary>
    /// <param name="name">The name of the options instance being configured.</param>
    /// <param name="options">The options instance to configure.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> or <paramref name="name"/> is <see langword="null"/></exception>
    public void PostConfigure(string? name, ProfileClaimsTransformerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.Cache ??= new EphermealProfileCache(loggerFactory, options.CacheTimeout);
    }
}
