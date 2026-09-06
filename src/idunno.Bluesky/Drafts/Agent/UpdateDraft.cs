// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Drafts;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Updates a draft for the authenticated user.
    /// </summary>
    /// <param name="draftWithId">The draft and ID to update.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="draftWithId"/> is <see langword="null"/>.</exception>
    public async Task<AtProtoHttpResult<EmptyResponse>> UpdateDraft(
        DraftWithId draftWithId,
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        ArgumentNullException.ThrowIfNull(draftWithId);

        return await BlueskyServer.UpdateDraft(
            draftWithId,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
