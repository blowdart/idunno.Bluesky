// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Exports the authenticated user's chat account data as JSON Lines.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the request.</param>
    /// <returns>The JSON Lines export result.</returns>
    /// <exception cref="idunno.AtProto.AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    public async Task<global::idunno.AtProto.AtProtoHttpResult<string>> ExportChatAccountData(CancellationToken cancellationToken = default)
    {
        if (!IsAuthenticated)
        {
            throw new idunno.AtProto.AuthenticationRequiredException();
        }

        return await BlueskyServer.ExportChatAccountData(
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            maximumResponseSize: MaximumResponseSize,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
