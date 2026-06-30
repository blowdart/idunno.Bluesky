// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Chat.Group;
using idunno.Bluesky.Chat.Group.Model;

using Microsoft.Extensions.Logging;

namespace idunno.Bluesky;

public partial class BlueskyServer
{
    /// <summary>
    /// Lists a page of request as a <see cref="PagedViewReadOnlyCollection{JoinRequestView}"/> to join a group (via join link) the user owns. Shows the data from the owner's point of view.
    /// </summary>
    /// <param name="conversationIds">The ids of the conversations to list join requests for.</param>
    /// <param name="cursor">An optional cursor used for pagination.</param>
    /// <param name="service">The <see cref="Uri"/> of the service to call.</param>
    /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
    /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
    /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
    /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="conversationIds"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="conversationIds"/> is empty or contains more than 100 items.</exception>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
    public static async Task<AtProtoHttpResult<PagedViewReadOnlyCollection<JoinRequestView>>> ListJoinRequests(
        ICollection<string> conversationIds,
        string? cursor,
        Uri service,
        AccessCredentials accessCredentials,
        HttpClient httpClient,
        Action<AtProtoCredential>? onCredentialsUpdated = null,
        ILoggerFactory? loggerFactory = default,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conversationIds);
        ArgumentOutOfRangeException.ThrowIfZero(conversationIds.Count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(conversationIds.Count, 100);

        string queryString = string.Join("&", conversationIds.Select(id => $"convoId={Uri.EscapeDataString(id)}"));

        if (cursor is not null)
        {
            queryString += $"&cursor={Uri.EscapeDataString(cursor)}";
        }

        BlueskyHttpClient<ListJoinRequestsResponse> client = new(ChatProxy, loggerFactory);

        AtProtoHttpResult<ListJoinRequestsResponse> response = await client.Get(
            service,
            $"/xrpc/chat.bsky.group.listJoinRequests?{queryString}",
            credentials: accessCredentials,
            httpClient: httpClient,
            jsonSerializerOptions: BlueskyJsonSerializerOptions,
            onCredentialsUpdated: onCredentialsUpdated,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        // Flatten into collection
        PagedViewReadOnlyCollection<JoinRequestView> result;
        if (response.Result is not null)
        {
            result = new PagedViewReadOnlyCollection<JoinRequestView>(response.Result.Requests, response.Result.Cursor);
        }
        else
        {
            result = new PagedViewReadOnlyCollection<JoinRequestView>();
        }

        return new AtProtoHttpResult<PagedViewReadOnlyCollection<JoinRequestView>>(
            result: result,
            statusCode: response.StatusCode,
            httpResponseHeaders: response.HttpResponseHeaders,
            atErrorDetail: response.AtErrorDetail,
            rateLimit: response.RateLimit);
    }
}