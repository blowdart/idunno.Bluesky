// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;
using idunno.Bluesky.Unspecced;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Gets curated Atmosphere Explore content with optional viewer-specific state.
    /// </summary>
    /// <param name="languages">The preferred languages, currently ignored by the service.</param>
    /// <param name="countryCode">The optional ISO 3166-1 country code.</param>
    /// <param name="regionCode">The optional ISO 3166-2 region code.</param>
    /// <param name="subscribedLabelers">The optional labeler identifiers.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The curated Explore content result.</returns>
    /// <exception cref="ArgumentNullException">A language entry is <see langword="null"/>.</exception>
    [Experimental("BSKYUnspecced", UrlFormat = "https://bluesky.idunno.dev/docs/unspecced.html")]
    public async Task<AtProtoHttpResult<AtmosphereExploreTab>> GetAtmosphereExploreTab(
        IEnumerable<string>? languages = null,
        string? countryCode = null,
        string? regionCode = null,
        IEnumerable<Did>? subscribedLabelers = null,
        CancellationToken cancellationToken = default)
    {
#pragma warning disable BSKYUnspecced
        return await BlueskyServer.GetAtmosphereExploreTab(
            languages,
            countryCode,
            regionCode,
            service: IsAuthenticated ? Service : ReadOnlyServiceUri,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            subscribedLabelers: subscribedLabelers,
            maximumResponseSize: MaximumResponseSize,
            cancellationToken: cancellationToken).ConfigureAwait(false);
#pragma warning restore BSKYUnspecced
    }
}
