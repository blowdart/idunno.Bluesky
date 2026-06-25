// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Actor;
using idunno.Bluesky.Chat;
using idunno.Bluesky.Chat.Model;
using idunno.Bluesky.RichText;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Accepts the conversation specified by the <paramref name="conversationId"/> for the authenticated user.
    /// </summary>
    /// <param name="conversationId">The conversation identifier to accept.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">
    ///   Thrown when <paramref name="conversationId"/> is <see langword="null"/> or whitespace
    /// </exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<AcceptConversationResponse>> AcceptConversation(
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await BlueskyServer.AcceptConversation(
            conversationId,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes the message specified by <paramref name="messageId"/> from the conversation identified by <paramref name="conversationId"/> for the authenticated user.
    /// </summary>
    /// <param name="conversationId">The conversation identifier to delete the message identified by <paramref name="messageId"/> from.</param>
    /// <param name="messageId">The message identifier to delete from <paramref name="conversationId"/>.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">
    ///   Thrown when <paramref name="conversationId"/> or <paramref name="messageId"/> is <see langword="null"/> or whitespace,
    /// </exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<DeletedMessageView>> DeleteMessageForSelf(
        string conversationId,
        string messageId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await BlueskyServer.DeleteMessageForSelf(
            conversationId,
            messageId,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Adds a reaction to the message identified by <paramref name="messageId"/> from the conversation identified by <paramref name="conversationId"/> for the authenticated user.
    /// </summary>
    /// <param name="conversationId">The conversation identifier to add the reaction to identified by <paramref name="messageId"/> from.</param>
    /// <param name="messageId">The message identifier to add the reaction to from <paramref name="conversationId"/>.</param>
    /// <param name="value">The reaction to add.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when any of <paramref name="conversationId"/>, <paramref name="messageId"/> or <paramref name="value"/> are <see langword="null"/> or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="value"/> has a grapheme length that does not equal 1.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<MessageView>> AddReaction(
        string conversationId,
        string messageId,
        string value,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        ArgumentOutOfRangeException.ThrowIfNotEqual(value.GetGraphemeLength(), 1);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await BlueskyServer.AddReaction(
            conversationId,
            messageId,
            value,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets a <see cref="ConversationView">view</see> over a conversation between <paramref name="members"/>.
    /// </summary>
    /// <param name="members">The <see cref="Did"/>s of the conversation members.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="members"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="members"/> is empty or has greater than the maximum number of conversation members.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<ConversationView>> GetConversationForMembers(
        ICollection<Did> members,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(members);

        ArgumentOutOfRangeException.ThrowIfZero(members.Count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(members.Count, Maximum.ConversationMembers);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await BlueskyServer.GetConversationForMembers(
            members,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Enumerates a list of conversations the current user is a part of.
    /// </summary>
    /// <param name="limit">The number of conversations to return.</param>
    /// <param name="cursor">A cursor used for pagination.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<Conversations>> ListConversations(
        int? limit = null,
        string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await BlueskyServer.ListConversations(
            limit,
            cursor,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets a <see cref="ConversationView">view</see> over conversation by its <paramref name="id"/>.
    /// </summary>
    /// <param name="id">The conversation identifier.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="id"/>is <see langword="null"/> or white space.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<ConversationView>> GetConversation(
        string id,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await BlueskyServer.GetConversation(
            id,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

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
    /// Gets the availability of a conversation between the authenticated user, and the user identified by <paramref name="member"/>.
    /// If an existing conversationg is found for these members, it is returned.
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
    /// Gets the availability of a conversation between the authenticated user, and the users identified by <paramref name="members"/>.
    /// If an existing conversationg is found for these members, it is returned.
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
    /// Gets the availability of a conversation between the authenticated user, and the users identified by <paramref name="members"/>.
    /// If an existing conversationg is found for these members, it is returned.
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

    /// <summary>
    /// Enumerates the conversation log.
    /// </summary>
    /// <param name="cursor">A cursor used for pagination.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<Logs>> GetConversationLog(
        string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await BlueskyServer.GetConversationLog(
            cursor,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets a <see cref="PagedViewReadOnlyCollection{ProfileViewBasic}"/> representing the members of a conversation.
    /// </summary>
    /// <param name="id">The identifier of the conversation.</param>
    /// <param name="limit">An optional limit on the number of members to retrieve in each page.</param>
    /// <param name="cursor">An optional cursor used for pagination.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<PagedViewReadOnlyCollection<ProfileViewBasic>>> GetConversationMembers(
        string id,
        int? limit = null,
        string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await BlueskyServer.GetConversationMembers(
            id,
            limit,
            cursor,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Enumerates the messages in a conversation.
    /// </summary>
    /// <param name="id">The conversation identifier whose messages should be retrieved.</param>
    /// <param name="limit">An optional limit on the number of messages to retrieve in each page.</param>
    /// <param name="cursor">An optional cursor used for pagination.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> is <see langword="null"/> or white space.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<Messages>> GetMessages(
        string id,
        int? limit = null,
        string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await BlueskyServer.GetMessages(
            id,
            limit,
            cursor,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the count of unread conversations for the authenticated user.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<UnreadConversationCounts>> GetUnreadCounts(
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await BlueskyServer.GetUnreadCounts(
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Leaves the conversation identified by <paramref name="id"/>.
    /// </summary>
    /// <param name="id">The conversation identifier to leave.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> is <see langword="null"/> or white space.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<ConversationReference>> LeaveConversation(
        string id,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await BlueskyServer.LeaveConversation(
            id,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Mutes the conversation identified by <paramref name="id"/>.
    /// </summary>
    /// <param name="id">The conversation identifier to mute.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> is <see langword="null"/> or white space.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<ConversationView>> MuteConversation(
        string id,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await BlueskyServer.MuteConversation(
            id,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Removes a reaction to the message identified by <paramref name="messageId"/> from the conversation identified by <paramref name="conversationId"/> for the authenticated user.
    /// </summary>
    /// <param name="conversationId">The conversation identifier to add the reaction to identified by <paramref name="messageId"/> from.</param>
    /// <param name="messageId">The message identifier to add the reaction to from <paramref name="conversationId"/>.</param>
    /// <param name="value">The reaction to add.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="conversationId"/>, <paramref name="messageId"/> or <paramref name="value"/> is whitespace.</exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="conversationId"/>, <paramref name="messageId"/> or <paramref name="value"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="value"/> has a grapheme length that does not equal 1.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<MessageView>> RemoveReaction(
        string conversationId,
        string messageId,
        string value,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        ArgumentOutOfRangeException.ThrowIfNotEqual(value.GetGraphemeLength(), 1);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await BlueskyServer.RemoveReaction(
            conversationId,
            messageId,
            value,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends the specified <paramref name="batchedMessages"/>.
    /// </summary>
    /// <param name="batchedMessages">The collection of <see cref="BatchedMessage"/>s to send.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="batchedMessages"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="batchedMessages"/> is empty or has greater than the maximum allowed number of messages.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<ICollection<MessageView>>> SendMessageBatch(
        ICollection<BatchedMessage> batchedMessages,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batchedMessages);
        ArgumentOutOfRangeException.ThrowIfZero(batchedMessages.Count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(batchedMessages.Count, Maximum.BatchedMessages);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await BlueskyServer.SendMessageBatch(
            batchedMessages,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends the specified <see cref="MessageInput"/> to the conversation identified by <paramref name="id"/>.
    /// </summary>
    /// <param name="id">The conversation identifier to send the <paramref name="message"/> to.</param>
    /// <param name="message">The message to send.</param>
    /// <param name="extractFacets">Flag indicating whether facets should be extracted from <paramref name="message" />.</param>
    /// <param name="embeddedPost">A <see cref="StrongReference"/> to a post that will be embedded in the message.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> is <see langword="null"/> or white space.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="message"/> is <see langword="null"/>, or if <paramref name="embeddedPost"/> is specified but its collection is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="embeddedPost"/> is specified but it is not in the <see cref="CollectionNsid.Post"/> collection.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<MessageView>> SendMessage(
        string id,
        string message,
        bool extractFacets = true,
        StrongReference? embeddedPost = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        ArgumentNullException.ThrowIfNull(message);

        if (embeddedPost is not null)
        {
            ArgumentNullException.ThrowIfNull(embeddedPost.Uri.Collection);
            ArgumentOutOfRangeException.ThrowIfNotEqual(embeddedPost.Uri.Collection, CollectionNsid.Post);
        }

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        MessageInput messageInput;

        if (!extractFacets)
        {
            messageInput = new MessageInput(message);
        }
        else
        {
            IList<Facet> facets = await FacetExtractor.ExtractFacets(message, cancellationToken).ConfigureAwait(false);
            messageInput = new MessageInput(message, facets);
        }

        if (embeddedPost is not null)
        {
            messageInput.Embed = new Embed.EmbeddedRecord(embeddedPost);
        }

        return await SendMessage(
            id,
            messageInput,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
    /// <summary>
    /// Sends the specified <see cref="MessageInput"/> to the conversation identified by <paramref name="id"/>.
    /// </summary>
    /// <param name="id">The conversation identifier to send the <paramref name="message"/> to.</param>
    /// <param name="message">The <see cref="MessageInput"/> to send.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> is <see langword="null"/> or white space.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="message"/> is <see langword="null"/>.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<MessageView>> SendMessage(
        string id,
        MessageInput message,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        ArgumentNullException.ThrowIfNull(message);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await BlueskyServer.SendMessage(
            id,
            message,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Unmutes the conversation identified by <paramref name="id"/>.
    /// </summary>
    /// <param name="id">The conversation identifier to unmute.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> is <see langword="null"/> or white space.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<ConversationView>> UnmuteConversation(
        string id,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await BlueskyServer.UnmuteConversation(
            id,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Marks a conversation, and optionally a message, as read.
    /// </summary>
    /// <param name="conversationId">The conversation identifier to mark as read.</param>
    /// <param name="messageId">The message identifier in the conversation identified by <paramref name="conversationId"/> to mark as read.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="conversationId"/> is <see langword="null"/> or white space or <paramref name="messageId"/> is empty or white space.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<ConversationView>> UpdateRead(
        string conversationId,
        string? messageId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);

        if (messageId is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        }

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await BlueskyServer.UpdateRead(
            conversationId,
            messageId,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the conversation declaration record for authenticated user.
    /// </summary>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<AtProtoRepositoryRecord<Chat.Actor.Declaration>>> GetConversationDeclaration()
    {
        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await GetConversationDeclaration(cancellationToken: default).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the conversation declaration record for authenticated user.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<AtProtoRepositoryRecord<Chat.Actor.Declaration>>> GetConversationDeclaration(CancellationToken cancellationToken)
    {
        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await GetConversationDeclaration(Did, cancellationToken).ConfigureAwait(false);
    }


    /// <summary>
    /// Gets the conversation declaration record for the specified <paramref name="did"/>.
    /// </summary>
    /// <param name="did">The <see cref="Did"/> whose conversation record should be retrieved.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="did"/> is <see langword="null"/>.</exception>
    public async Task<AtProtoHttpResult<AtProtoRepositoryRecord<Chat.Actor.Declaration>>> GetConversationDeclaration(Did did, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(did);

        return await
            GetBlueskyRecord<Chat.Actor.Declaration>($"at://{did}/{CollectionNsid.ChatDeclaration}/self", cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sets a conversation declaration record for the current user. Requires authentication.
    /// </summary>
    /// <param name="allowIncoming">Specifies whether incoming messages are allowed. Known values are specified in <see cref="Chat.Actor.AllowIncoming"/></param>
    /// <param name="allowGroupInvites">Specifies whether group invites are allowed. Known values are specified in <see cref="Chat.Actor.AllowGroupInvites"/></param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="allowIncoming"/> or <paramref name="allowGroupInvites"/> is <see langword="null"/> or whitespace.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current session is not authenticated.</exception>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Put().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Put().")]
    public async Task<AtProtoHttpResult<PutRecordResult>> SetConversationDeclaration(string allowIncoming, string allowGroupInvites)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(allowIncoming);
        ArgumentException.ThrowIfNullOrWhiteSpace(allowGroupInvites);

        return await SetConversationDeclaration(allowIncoming, allowGroupInvites, cancellationToken: default).ConfigureAwait(false);
    }

    /// <summary>
    /// Sets a conversation declaration record for the current user. Requires authentication.
    /// </summary>
    /// <param name="allowIncoming">Specifies whether incoming messages are allowed. Known values are specified in <see cref="Chat.Actor.AllowIncoming"/></param>
    /// <param name="allowGroupInvites">Specifies whether group invites are allowed. Known values are specified in <see cref="Chat.Actor.AllowGroupInvites"/></param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="allowIncoming"/> or <paramref name="allowGroupInvites"/> is <see langword="null"/> or whitespace.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current session is not authenticated.</exception>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Put().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Put().")]
    public async Task<AtProtoHttpResult<PutRecordResult>> SetConversationDeclaration(string allowIncoming, string allowGroupInvites, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(allowIncoming);
        ArgumentException.ThrowIfNullOrWhiteSpace(allowGroupInvites);

        var declaration = new Chat.Actor.Declaration(allowIncoming, allowGroupInvites);

        return await PutRecord(
            record: declaration,
            jsonSerializerOptions: BlueskyServer.BlueskyJsonSerializerOptions,
            collection: CollectionNsid.ChatDeclaration,
            rKey: "self",
            validate: null,
            swapCommit: null,
            swapRecord: null,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Update a conversation declaration record for the current user. Requires authentication.
    /// </summary>
    /// <param name="declaration">The declaration record to update.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="declaration"/> is <see langword="null"/>.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current session is not authenticated.</exception>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Put().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Put().")]
    [SuppressMessage("ApiDesign", "RS0027:API with optional parameter(s) should have the most parameters amongst its public overloads", Justification = "Using the strong record declaration as the default method.")]
    public async Task<AtProtoHttpResult<PutRecordResult>> SetConversationDeclaration(
        AtProtoRepositoryRecord<Chat.Actor.Declaration> declaration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(declaration);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await PutRecord(
            record: declaration.Value,
            jsonSerializerOptions: BlueskyServer.BlueskyJsonSerializerOptions,
            collection: CollectionNsid.ChatDeclaration,
            rKey: "self",
            validate: null,
            swapCommit: null,
            swapRecord: declaration.Cid,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}