// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Actor;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Gets <see cref="ProfileViewDetailed"/>s for the specified <paramref name="actors"/>.
    /// </summary>
    /// <param name="actors">A collection of <see cref="AtIdentifier"/>s of the actors to return <see cref="ProfileViewDetailed"/>s for.</param>
    /// <param name="subscribedLabelers">A optional list of labeler <see cref="Did"/>s to accept labels from.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="actors"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="actors"/> is an empty collection or if it contains &gt;25 handles.</exception>
    public async Task<AtProtoHttpResult<IReadOnlyCollection<ProfileViewDetailed>>> GetProfiles(
        IEnumerable<AtIdentifier> actors,
        IEnumerable<Did>? subscribedLabelers = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actors);

        var actorList = new List<AtIdentifier>(actors);

        if (actorList.Count == 0 || actorList.Count > 25)
        {
            ArgumentOutOfRangeException.ThrowIfZero(actorList.Count);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(actorList.Count, 25);
        }

        return await BlueskyServer.GetProfiles(
            actorList,
            AuthenticatedOrUnauthenticatedServiceUri,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            subscribedLabelers: subscribedLabelers,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
