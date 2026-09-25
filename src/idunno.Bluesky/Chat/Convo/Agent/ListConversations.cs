// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Chat;
using idunno.Bluesky.RichText;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Enumerates a list of conversations the current user is a part of.
    /// </summary>
    /// <param name="limit">The number of conversations to return.</param>
    /// <param name="cursor">A cursor used for pagination.</param>
    /// <param name="readState">An optional filter limiting the results to conversations in the specified read state. Known values are defined in <see cref="ConversationReadState"/>.</param>
    /// <param name="status">An optional filter limiting the results to conversations with the specified status. Known values are defined in <see cref="ConversationStatus"/>.</param>
    /// <param name="kind">An optional filter limiting the results to conversations of the specified kind. Known values are defined in <see cref="ConversationKind"/>.</param>
    /// <param name="lockStatus">An optional filter limiting the results to conversations with the specified lock status. Known values are defined in <see cref="ConversationLockStatus"/>.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when any of <paramref name="readState"/>, <paramref name="status"/>, <paramref name="kind"/> or <paramref name="lockStatus"/> is specified and is whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/>is &lt;1 or &gt; the maximum number of conversations to list.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    /// <remarks>
    /// <para>
    /// Setting <paramref name="status"/> to <see cref="ConversationStatus.Requested"/> is discouraged. Use
    /// <see cref="ListConversationRequests(int?, string?, CancellationToken)"/> instead, which also includes group join requests made by the user.
    /// </para>
    /// </remarks>
    public async Task<AtProtoHttpResult<Conversations>> ListConversations(
        int? limit = null,
        string? cursor = null,
        string? readState = null,
        string? status = null,
        string? kind = null,
        string? lockStatus = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await BlueskyServer.ListConversations(
            limit,
            cursor,
            readState,
            status,
            kind,
            lockStatus,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            maximumResponseSize: MaximumResponseSize,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}