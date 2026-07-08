// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Chat;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Enumerates the conversation log.
    /// </summary>
    /// <param name="cursor">A cursor to retrieve logs from.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    /// <remarks>
    /// <para>
    /// The getLogs endpoint has a different semantic to most other Bluesky API endpoints, it starts from "now". The intended use case is to use it to apply changes on top of some
    /// current state that you already have. To retrieve logs for the current user from the beginning of time use the zero-cursor, "2222222222222".
    /// </para>
    /// </remarks>
    public async Task<AtProtoHttpResult<Logs>> GetConversationLog(
        string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await BlueskyServer.GetConversationLog(
            cursor,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}