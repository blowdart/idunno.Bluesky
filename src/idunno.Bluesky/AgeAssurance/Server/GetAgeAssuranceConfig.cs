// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;
using idunno.Bluesky.AgeAssurance;

using Microsoft.Extensions.Logging;

namespace idunno.Bluesky;

public static partial class BlueskyServer
{
    /// <summary>
    /// Gets the age-assurance configuration.
    /// </summary>
    /// <param name="service">The service to get the configuration from.</param>
    /// <param name="httpClient">The HTTP client to use when making the request.</param>
    /// <param name="loggerFactory">A logger factory to use when creating a logger.</param>
    /// <param name="maximumResponseSize">The maximum number of bytes to read from the response body.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the request.</param>
    /// <returns>The age-assurance configuration result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="service"/> or <paramref name="httpClient"/> is <see langword="null"/>.</exception>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    public static async Task<AtProtoHttpResult<Config>> GetAgeAssuranceConfig(
        Uri service,
        HttpClient httpClient,
        ILoggerFactory? loggerFactory = default,
        int maximumResponseSize = AtProtoHttpClient.DefaultMaximumResponseSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(httpClient);

        BlueskyHttpClient<Config> client = new(AppViewProxy, loggerFactory) { MaximumResponseSize = maximumResponseSize };

        AtProtoHttpResult<Config> response = await client.Get(
            service,
            "/xrpc/app.bsky.ageassurance.getConfig",
            httpClient: httpClient,
            jsonSerializerOptions: BlueskyJsonSerializerOptions,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (response.Succeeded)
        {
            Config config = response.Result with
            {
                Regions = WithoutNullEntries(response.Result.Regions, service, nameof(Config.Regions), loggerFactory)
                    .Select(region => region with
                    {
                        Rules = WithoutNullEntries(region.Rules, service, nameof(ConfigRegion.Rules), loggerFactory)
                    }).ToList()
            };

            return new AtProtoHttpResult<Config>(
                config,
                response.StatusCode,
                response.HttpResponseHeaders,
                response.AtErrorDetail,
                response.RateLimit);
        }

        return response;
    }
}
