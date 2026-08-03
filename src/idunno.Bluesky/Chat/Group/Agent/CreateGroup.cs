// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Chat.Group.Model;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Creates a group conversation and invites members to join it.
    /// </summary>
    /// <param name="members">
    /// The members to add to the group. The owner is automatically added.
    /// Implementations may enforce a lower maximum than the 10,000-item schema limit; Bluesky currently supports up to 100 total members.
    /// If the owner is included in this list, the list may contain up to the implementation's total member limit. Otherwise, it may contain one fewer.
    /// </param>
    /// <param name="name">The name of the group conversation.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="members"/> or <paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="members"/> is empty or contains more than 49 members, or when <paramref name="name"/> exceeds the maximum length.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<CreateGroupResponse>> CreateGroup(
        ICollection<Did> members,
        string name,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(members);
        ArgumentOutOfRangeException.ThrowIfZero(members.Count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(members.Count, 10000);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(name.Length, 500);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(name.GetGraphemeLength(), 50);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await BlueskyServer.CreateGroup(
            members,
            name,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}