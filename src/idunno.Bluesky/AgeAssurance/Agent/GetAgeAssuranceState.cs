// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.Bluesky.AgeAssurance;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Gets age-assurance state for the specified country and optional region.
    /// </summary>
    /// <param name="countryCode">The country code used to compute age-assurance state.</param>
    /// <param name="regionCode">The optional region code used to compute age-assurance state.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the request.</param>
    /// <returns>The age-assurance state and metadata result.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="countryCode"/> is <see langword="null"/> or whitespace.</exception>
    /// <exception cref="idunno.AtProto.AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    public async Task<global::idunno.AtProto.AtProtoHttpResult<StateResult>> GetAgeAssuranceStateForRegion(
        string countryCode,
        string? regionCode = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(countryCode);

        if (!IsAuthenticated)
        {
            throw new idunno.AtProto.AuthenticationRequiredException();
        }

        return await BlueskyServer.GetAgeAssuranceStateForRegion(
            countryCode,
            regionCode,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            maximumResponseSize: MaximumResponseSize,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
