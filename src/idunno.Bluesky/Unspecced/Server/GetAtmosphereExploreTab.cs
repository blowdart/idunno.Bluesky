// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Unspecced;

using Microsoft.Extensions.Logging;

namespace idunno.Bluesky;

public static partial class BlueskyServer
{
    /// <summary>
    /// Gets curated Atmosphere Explore content.
    /// </summary>
    /// <param name="languages">The preferred languages, currently ignored by the service.</param>
    /// <param name="countryCode">The optional ISO 3166-1 country code.</param>
    /// <param name="regionCode">The optional ISO 3166-2 region code.</param>
    /// <param name="service">The AppView service.</param>
    /// <param name="accessCredentials">The optional credentials for viewer-specific state.</param>
    /// <param name="httpClient">The HTTP client.</param>
    /// <param name="onCredentialsUpdated">The credential update callback.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <param name="subscribedLabelers">The optional labeler identifiers.</param>
    /// <param name="maximumResponseSize">The maximum response size in bytes.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The curated Explore content result.</returns>
    /// <exception cref="ArgumentNullException">The service, HTTP client or a language entry is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The maximum response size is not positive.</exception>
    [Experimental("BSKYUnspecced", UrlFormat = "https://bluesky.idunno.dev/docs/unspecced.html")]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "AtmosphereExploreTab and its reachable modality views are registered in the source-generated context; AtmosphereExploreTests exercises this HTTP path with reflection disabled.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "AtmosphereExploreTab and its reachable modality views are registered in the source-generated context; AtmosphereExploreTests exercises this HTTP path with reflection disabled.")]
    public static async Task<AtProtoHttpResult<AtmosphereExploreTab>> GetAtmosphereExploreTab(
        IEnumerable<string>? languages,
        string? countryCode,
        string? regionCode,
        Uri service,
        AccessCredentials? accessCredentials,
        HttpClient httpClient,
        Func<AtProtoCredential, CancellationToken, Task>? onCredentialsUpdated = null,
        ILoggerFactory? loggerFactory = default,
        IEnumerable<Did>? subscribedLabelers = null,
        int maximumResponseSize = AtProtoHttpClient.DefaultMaximumResponseSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(httpClient);

        StringBuilder query = new();
        if (languages is not null)
        {
            foreach (string language in languages)
            {
                ArgumentNullException.ThrowIfNull(language);
                query.Append("langs=").Append(Uri.EscapeDataString(language)).Append('&');
            }
        }

        if (countryCode is not null)
        {
            query.Append("countryCode=").Append(Uri.EscapeDataString(countryCode)).Append('&');
        }

        if (regionCode is not null)
        {
            query.Append("regionCode=").Append(Uri.EscapeDataString(regionCode)).Append('&');
        }

        BlueskyHttpClient<AtmosphereExploreTab> request = new(AppViewProxy, loggerFactory) { MaximumResponseSize = maximumResponseSize };
        AtProtoHttpResult<AtmosphereExploreTab> response = await request.Get(
            service,
            $"/xrpc/app.bsky.unspecced.getAtmosphereExploreTab{BuildQueryString(query)}",
            credentials: accessCredentials,
            httpClient: httpClient,
            jsonSerializerOptions: BlueskyJsonSerializerOptions,
            onCredentialsUpdated: onCredentialsUpdated,
            subscribedLabelers: subscribedLabelers,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return response;
    }
}
