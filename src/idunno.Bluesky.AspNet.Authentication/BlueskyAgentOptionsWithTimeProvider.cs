// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.AspNet.Authentication;

internal static class BlueskyAgentOptionsWithTimeProvider
{
    internal static BlueskyAgentOptions Create(BlueskyAgentOptions configured, TimeProvider timeProvider)
    {
        BlueskyAgentOptions options = new()
        {
            EnableBackgroundTokenRefresh = configured.EnableBackgroundTokenRefresh,
            FacetExtractor = configured.FacetExtractor,
            HttpClientOptions = configured.HttpClientOptions,
            HttpJsonOptions = configured.HttpJsonOptions,
            LoggerFactory = configured.LoggerFactory,
            MaximumResponseSize = configured.MaximumResponseSize,
            MaximumWellKnownResponseSize = configured.MaximumWellKnownResponseSize,
            OAuthOptions = configured.OAuthOptions,
            PlcDirectoryServer = configured.PlcDirectoryServer,
            PublicAppViewUri = configured.PublicAppViewUri,
            TimeProvider = timeProvider
        };

        foreach (string root in configured.DraftMediaRoots)
        {
            options.DraftMediaRoots.Add(root);
        }

        return options;
    }
}
