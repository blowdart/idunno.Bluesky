// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Chat;
using idunno.Bluesky.RichText;

namespace idunno.Bluesky
{
    public partial class BlueskyAgent
    {
        /// <summary>
        /// Deletes the message specified by <paramref name="messageId"/> from the conversation identified by <paramref name="conversationId"/> for the authenticated user.
        /// </summary>
        /// <param name="conversationId">The conversation identifier to delete the message identified by <paramref name="messageId"/> from.</param>
        /// <param name="messageId">The message identifier to delete from <paramref name="conversationId"/>.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">
        ///   Thrown when <paramref name="conversationId"/> or <paramref name="messageId"/> is null or whitespace,
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
        /// Gets a <see cref="ConversationView">view</see> over a conversation between <paramref name="members"/>.
        /// </summary>
        /// <param name="members">The <see cref="Did"/>s of the conversation members.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="members"/> is null.</exception>
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
        /// <exception cref="ArgumentException">Thrown when <paramref name="id"/>is null or white space.</exception>
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
        /// Enumerates the messages in a conversation.
        /// </summary>
        /// <param name="id">The conversation identifier whose messages should be retrieved.</param>
        /// <param name="limit">An optional limit on the number of messages to retrieve in each page.</param>
        /// <param name="cursor">An optional cursor used for pagination.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> is null or white space.</exception>
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
        /// Leaves the conversation identified by <paramref name="id"/>.
        /// </summary>
        /// <param name="id">The conversation identifier to leave.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> is null or white space.</exception>
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
        /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> is null or white space.</exception>
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
        /// Sends the specified <paramref name="batchedMessages"/>.
        /// </summary>
        /// <param name="batchedMessages">The collection of <see cref="BatchedMessage"/>s to send.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="batchedMessages"/> is null.</exception>
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
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> is null or white space.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="message"/> is null.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<MessageView>> SendMessage(
            string id,
            string message,
            bool extractFacets = true,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(id);

            ArgumentNullException.ThrowIfNull(message);

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
                IList<Facet> facets = await _facetExtractor.ExtractFacets(message, cancellationToken).ConfigureAwait(false);
                messageInput = new MessageInput(message, facets);
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
        /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> is null or white space.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="message"/> is null.</exception>
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
        /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> is null or white space.</exception>
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
        /// <exception cref="ArgumentException">Thrown when <paramref name="conversationId"/> is null or white space or <paramref name="messageId"/> is empty or white space.</exception>
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
    }
}
