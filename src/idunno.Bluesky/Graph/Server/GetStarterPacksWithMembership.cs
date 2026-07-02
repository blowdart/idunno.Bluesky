// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Graph;
using idunno.Bluesky.Graph.Model;

using Microsoft.Extensions.Logging;

namespace idunno.Bluesky;

public partial class BlueskyServer
{
    /// <summary>
    /// Enumerates the starter packs created by the authenticated user, and includes membership information about <paramref name="actor"/> in those starter packs. Requires auth.
    /// </summary>
    /// <param name="actor">The account (actor) to check for membership.</param>
    /// <param name="limit">The maximum number of lists that should be return in a page.</param>
    /// <param name="cursor">An optional cursor for pagination.</param>
    /// <param name="service">The <see cref="Uri"/> of the service to get starter packs from.</param>
    /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
    /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
    /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
    /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="actor"/>, <paramref name="service"/>, <paramref name="httpClient"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is less than 1 or greater than 100.</exception>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
    public static async Task<AtProtoHttpResult<PagedViewReadOnlyCollection<StarterPackWithMembership>>> GetStarterPacksWithMembership(
        AtIdentifier actor,
        int? limit,
        string? cursor,
        Uri service,
        AccessCredentials accessCredentials,
        HttpClient httpClient,
        Action<AtProtoCredential>? onCredentialsUpdated = null,
        ILoggerFactory? loggerFactory = default,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (limit.HasValue)
        {
            ArgumentOutOfRangeException.ThrowIfZero(limit.Value);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(limit.Value, 100);
        }
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(accessCredentials);
        ArgumentNullException.ThrowIfNull(httpClient);

        StringBuilder queryStringBuilder = new();
        queryStringBuilder.Append(CultureInfo.InvariantCulture, $"actor={Uri.EscapeDataString(actor.ToString())}");
        if (limit.HasValue)
        {
            queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&limit={limit.Value}");
        }
        if (!string.IsNullOrEmpty(cursor))
        {
            queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&cursor={Uri.EscapeDataString(cursor)}");
        }

        string queryString = queryStringBuilder.ToString();

        BlueskyHttpClient<GetStarterPacksWithMembershipResponse> client = new(AppViewProxy, loggerFactory);

        AtProtoHttpResult<GetStarterPacksWithMembershipResponse> response = await client.Get(
            service,
            $"/xrpc/app.bsky.graph.getStarterPacksWithMembership?{queryString}",
            requestHeaders: null,
            credentials: accessCredentials,
            httpClient: httpClient,
            jsonSerializerOptions: BlueskyJsonSerializerOptions,
            onCredentialsUpdated: onCredentialsUpdated,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (response.Succeeded)
        {
            return new AtProtoHttpResult<PagedViewReadOnlyCollection<StarterPackWithMembership>>(
                new PagedViewReadOnlyCollection<StarterPackWithMembership>(response.Result.StarterPacksWithMembership, response.Result.Cursor),
                response.StatusCode,
                response.HttpResponseHeaders,
                response.AtErrorDetail,
                response.RateLimit);
        }
        else
        {
            return new AtProtoHttpResult<PagedViewReadOnlyCollection<StarterPackWithMembership>>(
                null,
                response.StatusCode,
                response.HttpResponseHeaders,
                response.AtErrorDetail,
                response.RateLimit);
        }
    }
}