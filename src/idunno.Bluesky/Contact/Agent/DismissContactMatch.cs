// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Dismisses a contact match so that reimporting the contact does not restore it.
    /// </summary>
    /// <param name="subject">The DID of the contact match to dismiss.</param>
    /// <param name="cancellationToken">A cancellation token for the operation.</param>
    /// <returns>The task representing the authenticated contact request.</returns>
    /// <exception cref="ArgumentNullException">An argument documented as required is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The maximum response size is not positive.</exception>
    /// <exception cref="AuthenticationRequiredException">The agent is not authenticated.</exception>
    /// <remarks>
    /// <para>Requires authentication. Named errors: <c>InvalidDid</c>, <c>InternalError</c>.</para>
    /// </remarks>
    public async Task<AtProtoHttpResult<EmptyResponse>> DismissContactMatch(
        Did subject,
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await BlueskyServer.DismissContactMatch(
            subject: subject,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            maximumResponseSize: MaximumResponseSize,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
