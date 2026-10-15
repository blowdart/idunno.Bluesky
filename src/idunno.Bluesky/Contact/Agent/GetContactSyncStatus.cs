// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Contact;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Gets the current contact synchronization status.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token for the operation.</param>
    /// <returns>The task representing the authenticated contact request.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The maximum response size is not positive.</exception>
    /// <exception cref="AuthenticationRequiredException">The agent is not authenticated.</exception>
    /// <remarks>
    /// <para>Requires authentication. Named errors: <c>InvalidDid</c>, <c>InternalError</c>.</para>
    /// </remarks>
    public async Task<AtProtoHttpResult<ContactSyncStatusResult>> GetContactSyncStatus(
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await BlueskyServer.GetContactSyncStatus(
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            maximumResponseSize: MaximumResponseSize,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
