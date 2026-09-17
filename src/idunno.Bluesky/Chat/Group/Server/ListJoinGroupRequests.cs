// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Chat.Group;
using idunno.Bluesky.Chat.Group.Model;

using Microsoft.Extensions.Logging;

namespace idunno.Bluesky;

public partial class BlueskyServer
{
    /// <summary>
    /// Lists a page of requests as a <see cref="PagedViewReadOnlyCollection{JoinRequestView}"/> to join a group (via join link) the user owns. Shows the data from the owner's point of view.
    /// </summary>
    /// <param name="conversationId">The id of the conversation to list join requests for.</param>
    /// <param name="limit">The maximum number of join requests to return. Must be between 1 and 100.</param>
    /// <param name="cursor">An optional cursor used for pagination.</param>
    /// <param name="service">The <see cref="Uri"/> of the service to call.</param>
    /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
    /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
    /// <param name="onCredentialsUpdated">An <see cref="Func{T1, T2, TResult}" /> to await if the credentials in the request need updating.</param>
    /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
    /// <param name="maximumResponseSize">The maximum number of bytes to read from the response body.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="conversationId"/>, <paramref name="service"/>, <paramref name="accessCredentials"/> or <paramref name="httpClient"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="conversationId"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is out of range.</exception>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
    public static async Task<AtProtoHttpResult<PagedViewReadOnlyCollection<JoinRequestView>>> ListJoinGroupRequests(
        string conversationId,
        int? limit,
        string? cursor,
        Uri service,
        AccessCredentials accessCredentials,
        HttpClient httpClient,
        Func<AtProtoCredential, CancellationToken, Task>? onCredentialsUpdated = null,
        ILoggerFactory? loggerFactory = default,
        int maximumResponseSize = AtProtoHttpClient.DefaultMaximumResponseSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(conversationId);

        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(accessCredentials);
        ArgumentNullException.ThrowIfNull(httpClient);

        if (limit is not null)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan((int)limit, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan((int)limit, Maximum.JoinRequestsToList);
        }

        StringBuilder queryStringBuilder = new();
        queryStringBuilder.Append(CultureInfo.InvariantCulture, $"convoId={Uri.EscapeDataString(conversationId)}");

        if (cursor is not null)
        {
            queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&cursor={Uri.EscapeDataString(cursor)}");
        }

        if (limit is not null)
        {
            queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&limit={limit}");
        }

        string queryString = queryStringBuilder.ToString();

        BlueskyHttpClient<ListJoinRequestsResponse> client = new(ChatProxy, loggerFactory) { MaximumResponseSize = maximumResponseSize };

        AtProtoHttpResult<ListJoinRequestsResponse> response = await client.Get(
            service,
            $"/xrpc/chat.bsky.group.listJoinRequests?{queryString}",
            credentials: accessCredentials,
            httpClient: httpClient,
            jsonSerializerOptions: BlueskyJsonSerializerOptions,
            onCredentialsUpdated: onCredentialsUpdated,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        // Flatten into collection
        PagedViewReadOnlyCollection<JoinRequestView>? result = response.Result is not null
            ? new PagedViewReadOnlyCollection<JoinRequestView>(WithoutNullEntries(response.Result.Requests, service, nameof(response.Result.Requests), loggerFactory), response.Result.Cursor)
            : null;

        return new AtProtoHttpResult<PagedViewReadOnlyCollection<JoinRequestView>>(
            result: result,
            statusCode: response.StatusCode,
            httpResponseHeaders: response.HttpResponseHeaders,
            atErrorDetail: response.AtErrorDetail,
            rateLimit: response.RateLimit);
    }
}
