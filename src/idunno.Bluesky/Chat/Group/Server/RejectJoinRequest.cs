// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Chat.Group.Model;

using Microsoft.Extensions.Logging;

namespace idunno.Bluesky;

public partial class BlueskyServer
{
    /// <summary>
    /// Rejects a request to join a group (via join link) the user owns. 
    /// </summary>
    /// <param name="conversationId">The id of the group to reject the join request for.</param>
    /// <param name="member">The <see cref="Did"/> of the user to reject the join request for.</param>
    /// <param name="service">The <see cref="Uri"/> of the service to call.</param>
    /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
    /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
    /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
    /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="conversationId"/> or <paramref name="member"/> is <see langword="null"/>.</exception>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
    public static async Task<AtProtoHttpResult<EmptyResponse>> RejectJoinRequest(
        string conversationId,
        Did member,
        Uri service,
        AccessCredentials accessCredentials,
        HttpClient httpClient,
        Action<AtProtoCredential>? onCredentialsUpdated = null,
        ILoggerFactory? loggerFactory = default,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conversationId);
        ArgumentNullException.ThrowIfNull(member);

        AtProtoHttpClient<EmptyResponse> client = new(ChatProxy, loggerFactory);

        AtProtoHttpResult<EmptyResponse> response = await client.Post(
            service,
            $"/xrpc/chat.bsky.group.rejectJoinRequest",
            new RejectJoinRequestRequest(conversationId, member),
            credentials: accessCredentials,
            httpClient: httpClient,
            jsonSerializerOptions: BlueskyJsonSerializerOptions,
            onCredentialsUpdated: onCredentialsUpdated,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return new AtProtoHttpResult<EmptyResponse>(
            result: response.Result,
            statusCode: response.StatusCode,
            httpResponseHeaders: response.HttpResponseHeaders,
            atErrorDetail: BlueskyError.Map(response.AtErrorDetail),
            rateLimit: response.RateLimit);
    }
}
