// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Deletes a draft by ID.
    /// </summary>
    /// <param name="draftId">The ID of the draft to delete.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="draftId"/> is <see langword="null"/>.</exception>
    public async Task<AtProtoHttpResult<EmptyResponse>> DeleteDraft(
        TimestampIdentifier draftId,
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        ArgumentNullException.ThrowIfNull(draftId);

        return await BlueskyServer.DeleteDraft(
            draftId,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}