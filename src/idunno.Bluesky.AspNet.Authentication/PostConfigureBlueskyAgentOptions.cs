// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto.Authentication;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace idunno.Bluesky.AspNet.Authentication;

/// <summary>
/// Initializes a new instance of <see cref="PostConfigureBlueskyAgentOptions"/> used to set default options..
/// </summary>
/// <param name="loggerFactory">The <see cref="ILoggerFactory"/> agents built from these options should create loggers from.</param>
public class PostConfigureBlueskyAgentOptions(ILoggerFactory loggerFactory) : IPostConfigureOptions<BlueskyAgentOptions>
{
    /// <summary>
    /// Invoked to post configure a TOptions instance.
    /// </summary>
    /// <param name="name">The name of the options instance being configured.</param>
    /// <param name="options">The options instance to configure.</param>
    [SuppressMessage("Minor Code Smell", "S1075:URIs should not be hardcoded", Justification = "Special hardcoded case for localhost.")]
    public void PostConfigure(string? name, BlueskyAgentOptions options)
    {
        // As the agents are likely scoped to the request, we don't want to enable background token refresh as it will likely be disposed before the refresh completes.
        options?.EnableBackgroundTokenRefresh = false;

        // Applied here, once, rather than being written to the shared options instance by BlueskyAgentFactory, which
        // would mutate an object the options system owns and hand every agent the same mutated instance.
        if (options is not null)
        {
            options.LoggerFactory ??= loggerFactory;
        }

        // Bluesky OAuth special cases a client identifier of http://localhost, requiring the return URI to be http://127.0.0.1.
        // The default is applied here, once, rather than being written to the shared options instance from a request thread.
        if (options?.OAuthOptions is OAuthOptions oAuthOptions &&
            oAuthOptions.ReturnUri is null &&
            oAuthOptions.ClientId.StartsWith("http://localhost", StringComparison.InvariantCulture))
        {
            oAuthOptions.ReturnUri = new Uri("http://127.0.0.1/Account/Callback");
        }
    }
}
