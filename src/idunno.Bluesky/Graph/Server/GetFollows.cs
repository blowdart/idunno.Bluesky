// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Actor;
using idunno.Bluesky.Graph;
using idunno.Bluesky.Graph.Model;

using Microsoft.Extensions.Logging;

namespace idunno.Bluesky;

public static partial class BlueskyServer
{
    /// <summary>
    /// Gets a paged list of accounts whom an actor follows.
    /// </summary>
    /// <param name="actor">The <see cref="AtIdentifier"/> of the actor whose follows should be enumerated.</param>
    /// <param name="limit">The maximum number of follows that should be returned in a page.</param>
    /// <param name="cursor">An optional cursor for pagination.</param>
    /// <param name="sort">An optional sort order. Known values are "latest" and "top".</param>
    /// <param name="service">The <see cref="Uri"/> of the service to retrieve the follows from.</param>
    /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
    /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
    /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
    /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
    /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels from.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="actor"/>, <paramref name="service"/> or <paramref name="httpClient"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is &lt; 1 or &gt; 100.</exception>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    public static async Task<AtProtoHttpResult<Follows>> GetFollows(
        AtIdentifier actor,
        int? limit,
        string? cursor,
        string? sort,
        Uri service,
        AccessCredentials? accessCredentials,
        HttpClient httpClient,
        Action<AtProtoCredential>? onCredentialsUpdated,
        ILoggerFactory? loggerFactory = default,
        IEnumerable<Did>? subscribedLabelers = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(httpClient);

        if (limit is not null)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan((int)limit, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan((int)limit, 100);
        }

        StringBuilder queryStringBuilder = new();
        queryStringBuilder.Append(CultureInfo.InvariantCulture, $"actor={Uri.EscapeDataString(actor.ToString())}");

        if (limit is not null)
        {
            queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&limit={limit}");
        }

        if (cursor is not null)
        {
            queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&cursor={Uri.EscapeDataString(cursor)}");
        }

        if (sort is not null)
        {
            queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&sort={Uri.EscapeDataString(sort)}");
        }

        string queryString = queryStringBuilder.ToString();

        BlueskyHttpClient<GetFollowsResponse> client = new(AppViewProxy, loggerFactory);
        AtProtoHttpResult<GetFollowsResponse> response = await client.Get(
            service,
            $"/xrpc/app.bsky.graph.getFollows?{queryString}",
            credentials: accessCredentials,
            httpClient: httpClient,
            jsonSerializerOptions: BlueskyJsonSerializerOptions,
            onCredentialsUpdated: onCredentialsUpdated,
            subscribedLabelers: subscribedLabelers,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (response.Succeeded)
        {
            return new AtProtoHttpResult<Follows>(
                new Follows(
                    subject: response.Result.Subject,
                    follows: new List<ProfileView>(response.Result.Follows).AsReadOnly(),
                    cursor: response.Result.Cursor),
                response.StatusCode,
                response.HttpResponseHeaders,
                response.AtErrorDetail,
                response.RateLimit);
        }
        else
        {
            return new AtProtoHttpResult<Follows>(
                new Follows(subject: null, follows: [], null),
                response.StatusCode,
                response.HttpResponseHeaders,
                response.AtErrorDetail,
                response.RateLimit);
        }
    }
}