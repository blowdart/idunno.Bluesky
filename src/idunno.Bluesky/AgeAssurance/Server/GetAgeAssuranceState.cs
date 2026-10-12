// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.AgeAssurance;

using Microsoft.Extensions.Logging;

namespace idunno.Bluesky;

public static partial class BlueskyServer
{
    /// <summary>
    /// Gets the age-assurance state for the specified country and optional region.
    /// </summary>
    /// <param name="countryCode">The country code used to compute age-assurance state.</param>
    /// <param name="regionCode">The optional region code used to compute age-assurance state.</param>
    /// <param name="service">The service to get the state from.</param>
    /// <param name="accessCredentials">The credentials used to authenticate to <paramref name="service"/>.</param>
    /// <param name="httpClient">The HTTP client to use when making the request.</param>
    /// <param name="onCredentialsUpdated">A callback to invoke if the credentials are updated during the request.</param>
    /// <param name="loggerFactory">A logger factory to use when creating a logger.</param>
    /// <param name="maximumResponseSize">The maximum number of bytes to read from the response body.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the request.</param>
    /// <returns>The age-assurance state and metadata result.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="countryCode"/> is <see langword="null"/> or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="service"/>, <paramref name="accessCredentials"/> or <paramref name="httpClient"/> is <see langword="null"/>.</exception>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    public static async Task<AtProtoHttpResult<StateResult>> GetAgeAssuranceStateForRegion(
        string countryCode,
        string? regionCode,
        Uri service,
        AccessCredentials accessCredentials,
        HttpClient httpClient,
        Func<AtProtoCredential, CancellationToken, Task>? onCredentialsUpdated = null,
        ILoggerFactory? loggerFactory = default,
        int maximumResponseSize = AtProtoHttpClient.DefaultMaximumResponseSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(countryCode);
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(accessCredentials);
        ArgumentNullException.ThrowIfNull(httpClient);

        string requestUri = $"/xrpc/app.bsky.ageassurance.getState?countryCode={Uri.EscapeDataString(countryCode)}";

        if (regionCode is not null)
        {
            requestUri += $"&regionCode={Uri.EscapeDataString(regionCode)}";
        }

        BlueskyHttpClient<StateResult> client = new(AppViewProxy, loggerFactory) { MaximumResponseSize = maximumResponseSize };

        return await client.Get(
            service,
            requestUri,
            credentials: accessCredentials,
            httpClient: httpClient,
            jsonSerializerOptions: BlueskyJsonSerializerOptions,
            onCredentialsUpdated: onCredentialsUpdated,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
