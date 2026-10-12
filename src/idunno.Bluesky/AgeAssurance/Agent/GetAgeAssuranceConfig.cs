// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.AgeAssurance;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Gets the age-assurance configuration.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the request.</param>
    /// <returns>The age-assurance configuration result.</returns>
    public async Task<AtProtoHttpResult<Config>> GetAgeAssuranceConfig(CancellationToken cancellationToken = default)
    {
        return await BlueskyServer.GetAgeAssuranceConfig(
            service: Service,
            httpClient: HttpClient,
            loggerFactory: LoggerFactory,
            maximumResponseSize: MaximumResponseSize,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
