// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Graph;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Enumerates public relationships between one account, and a list of other accounts.
    /// </summary>
    /// <param name="actor">The <see cref="AtIdentifier"/> of the actor whose follows should be enumerated.</param>
    /// <param name="others">A list of other accounts to be related back to <paramref name="actor"/>.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="actor"/> or <paramref name="others"/> is <see langword="null"/>.</exception>
    public async Task<AtProtoHttpResult<RelationshipMap>> GetRelationships(
        AtIdentifier actor,
        ICollection<AtIdentifier> others,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(others);

        return await BlueskyServer.GetRelationships(
            actor,
            others,
            service: AuthenticatedOrUnauthenticatedServiceUri,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Enumerates public relationships between the current account, and a list of other accounts.
    /// </summary>
    /// <param name="others">A list of other accounts to be related back to current actor.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="others"/> is <see langword="null"/>.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<RelationshipMap>> GetRelationships(
        ICollection<AtIdentifier> others)
    {
        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        ArgumentNullException.ThrowIfNull(others);

        return await GetRelationships(
            Did,
            others,
            cancellationToken: default).ConfigureAwait(false);
    }

    /// <summary>
    /// Enumerates public relationships between the current account, and a list of other accounts.
    /// </summary>
    /// <param name="others">A list of other accounts to be related back to current actor.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="others"/> is <see langword="null"/>.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<RelationshipMap>> GetRelationships(
        ICollection<AtIdentifier> others,
        CancellationToken cancellationToken)
    {
        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        ArgumentNullException.ThrowIfNull(others);

        return await GetRelationships(
            Did,
            others,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}