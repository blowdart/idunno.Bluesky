// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Options;

namespace idunno.Bluesky.AspNet.Authentication;

/// <summary>
/// Initializes a new instance of <see cref="PostConfigureBlueksyAgentOptions"/> used to set default options..
/// </summary>
public class PostConfigureBlueksyAgentOptions() : IPostConfigureOptions<BlueskyAgentOptions>
{
    /// <summary>
    /// Invoked to post configure a TOptions instance.
    /// </summary>
    /// <param name="name">The name of the options instance being configured.</param>
    /// <param name="options">The options instance to configure.</param>
    public void PostConfigure(string? name, BlueskyAgentOptions options)
    {
        // As the agents are likely scoped to the request, we don't want to enable background token refresh as it will likely be disposed before the refresh completes.
        options?.EnableBackgroundTokenRefresh = false;
    }
}
