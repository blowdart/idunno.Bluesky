// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Chat;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Gets the availability of a conversation between the authenticated user, and the user identified by <paramref name="member"/>.
    /// If an existing conversationg is found for these members, it is returned.
    /// </summary>
    /// <param name="member">The <see cref="Did"/> of the actor to check availability for.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="member"/> is <see langword="null"/>.</exception>
    public async Task<AtProtoHttpResult<ConversationAvailability>> GetConversationAvailability(
        Did member)
    {
        ArgumentNullException.ThrowIfNull(member);
        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await GetConversationAvailability(
            [member],
            cancellationToken: default).ConfigureAwait(false);
    }

    /// <summary>
    /// Check whether the requester and <paramref name="member"/> can start a 1-1 chat. Only applicable to direct (non-group) conversations.
    /// If an existing conversation is found for the <paramref name="member"/>, it is returned. Does not create a new conversation if it doesn't exist.
    /// </summary>
    /// <param name="member">The <see cref="Did"/> of the actor to check availability for.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="member"/> is <see langword="null"/>.</exception>
    public async Task<AtProtoHttpResult<ConversationAvailability>> GetConversationAvailability(
        Did member,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(member);
        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await GetConversationAvailability(
            [member],
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Check whether the requester and the other <paramref name="members"/> can start a 1-1 chat. Only applicable to direct (non-group) conversations.
    /// If an existing conversation is found for these <paramref name="members"/>, it is returned. Does not create a new conversation if it doesn't exist.
    /// </summary>
    /// <param name="members">A collection of <see cref="Did"/> of actors to check availability for.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="members"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="members"/> is empty or contains more than the maximum allowed members.</exception>
    public async Task<AtProtoHttpResult<ConversationAvailability>> GetConversationAvailability(
        ICollection<Did> members)
    {
        ArgumentNullException.ThrowIfNull(members);
        ArgumentOutOfRangeException.ThrowIfZero(members.Count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(members.Count, Maximum.ConversationMembers);
        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await GetConversationAvailability(
            members,
            cancellationToken: default).ConfigureAwait(false);
    }

    /// <summary>
    /// Check whether the requester and the other <paramref name="members"/> can start a 1-1 chat. Only applicable to direct (non-group) conversations.
    /// If an existing conversation is found for these <paramref name="members"/>, it is returned. Does not create a new conversation if it doesn't exist.
    /// </summary>
    /// <param name="members">A collection of <see cref="Did"/> of actors to check availability for.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="members"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="members"/> is empty or contains more than the maximum allowed members.</exception>
    public async Task<AtProtoHttpResult<ConversationAvailability>> GetConversationAvailability(
        ICollection<Did> members,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(members);
        ArgumentOutOfRangeException.ThrowIfZero(members.Count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(members.Count, Maximum.ConversationMembers);
        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await BlueskyServer.GetConversationAvailability(
            members,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

}
