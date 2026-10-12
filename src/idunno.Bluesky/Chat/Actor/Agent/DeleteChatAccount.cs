// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Deletes the authenticated user's chat account.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the request.</param>
    /// <returns>The result of the delete request.</returns>
    /// <exception cref="idunno.AtProto.AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    public async Task<global::idunno.AtProto.AtProtoHttpResult<global::idunno.AtProto.EmptyResponse>> DeleteChatAccount(CancellationToken cancellationToken = default)
    {
        if (!IsAuthenticated)
        {
            throw new idunno.AtProto.AuthenticationRequiredException();
        }

        return await BlueskyServer.DeleteChatAccount(
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            maximumResponseSize: MaximumResponseSize,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
