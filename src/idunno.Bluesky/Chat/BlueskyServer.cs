// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

using Microsoft.Extensions.Logging;

using idunno.AtProto;
using idunno.Bluesky.Chat;
using idunno.Bluesky.Chat.Model;
using idunno.AtProto.Authentication;

namespace idunno.Bluesky
{
    /// <summary>
    /// Wraps the /xrpc/chat.bsky.convo.* APIs
    /// </summary>
    public static partial class BlueskyServer
    {
        // https://docs.bsky.app/docs/api/chat-bsky-convo-accept-convo
        private const string AcceptConvoEndpoint = "/xrpc/chat.bsky.convo.acceptConvo";

        private const string AddReactionEndpoint = "/xrpc/chat.bsky.convo.addReaction";

        // https://docs.bsky.app/docs/api/chat-bsky-convo-delete-message-for-self
        private const string DeleteMessageForSelfEndpoint = "/xrpc/chat.bsky.convo.deleteMessageForSelf";

        // https://docs.bsky.app/docs/api/chat-bsky-convo-get-convo-for-members
        private const string GetConvoForMembersEndpoint = "/xrpc/chat.bsky.convo.getConvoForMembers";

        // https://docs.bsky.app/docs/api/chat-bsky-convo-get-convo
        private const string GetConvoEndpoint = "/xrpc/chat.bsky.convo.getConvo";

        // https://docs.bsky.app/docs/api/chat-bsky-convo-list-convos
        private const string ListConvosEndpoint = "/xrpc/chat.bsky.convo.listConvos";

        // https://docs.bsky.app/docs/api/chat-bsky-convo-get-log
        private const string GetLogEndpoint = "/xrpc/chat.bsky.convo.getLog";

        // https://docs.bsky.app/docs/api/chat-bsky-convo-get-messages
        private const string GetMessagesEndpoint = "/xrpc/chat.bsky.convo.getMessages";

        // https://docs.bsky.app/docs/api/chat-bsky-convo-leave-convo
        private const string LeaveConvoEndpoint = "/xrpc/chat.bsky.convo.leaveConvo";

        // https://docs.bsky.app/docs/api/chat-bsky-convo-mute-convo
        private const string MuteConvoEndpoint = "/xrpc/chat.bsky.convo.muteConvo";

        private const string RemoveReactionEndpoint = "/xrpc/chat.bsky.convo.removeReaction";

        // https://docs.bsky.app/docs/api/chat-bsky-convo-send-message-batch
        private const string SendMessageBatchEndpoint = "/xrpc/chat.bsky.convo.sendMessageBatch";

        // https://docs.bsky.app/docs/api/chat-bsky-convo-send-message
        private const string SendMessageEndpoint = "/xrpc/chat.bsky.convo.sendMessage";

        // https://docs.bsky.app/docs/api/chat-bsky-convo-unmute-convo
        private const string UnmuteConvoEndpoint = "/xrpc/chat.bsky.convo.unmuteConvo";

        // https://docs.bsky.app/docs/api/chat-bsky-convo-update-all-read
        private const string UpdateAllReadEndpoint = "/xrpc/chat.bsky.convo.updateRead";

        // https://docs.bsky.app/docs/api/chat-bsky-convo-update-read
        private const string UpdateReadEndpoint = "/xrpc/chat.bsky.convo.updateRead";

        private const string ChatProxy = "did:web:api.bsky.chat#bsky_chat";

        /// <summary>
        /// Accepts the conversation specified by the <paramref name="conversationId"/> for the authenticated user.
        /// </summary>
        /// <param name="conversationId">The conversation identifier to accept.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to accept the conversation on.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> to use when accessing the <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="conversationId"/> is whitespace.</exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when any of <paramref name="conversationId"/>, <paramref name="accessCredentials"/>, <paramref name="service"/> or <paramref name="httpClient"/> are <see langword="null"/>.
        /// </exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
        public static async Task<AtProtoHttpResult<AcceptConversationResponse>> AcceptConversation(
            string conversationId,
            Uri service,
            AccessCredentials accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);

            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentNullException.ThrowIfNull(httpClient);

            AcceptConversationRequest request = new (conversationId);

            AtProtoHttpClient<AcceptConversationResponse> client = new(ChatProxy, loggerFactory);

            AtProtoHttpResult<AcceptConversationResponse> response = await client.Post(
                service,
                AcceptConvoEndpoint,
                request,
                credentials: accessCredentials,
                httpClient: httpClient,
                jsonSerializerOptions: BlueskyJsonSerializerOptions,
                onCredentialsUpdated: onCredentialsUpdated,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            return response;
        }

        /// <summary>
        /// Adds a reaction to the message identified by <paramref name="messageId"/> from the conversation identified by <paramref name="conversationId"/> for the authenticated user.
        /// </summary>
        /// <param name="conversationId">The conversation identifier to add the reaction to identified by <paramref name="messageId"/> from.</param>
        /// <param name="messageId">The message identifier to add the reaction to from <paramref name="conversationId"/>.</param>
        /// <param name="value">The reaction to add.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to delete the message from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> to use when accessing the <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="conversationId"/>, <paramref name="messageId"/> or <paramref name="value"/> is whitespace.</exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when any of <paramref name="conversationId"/>, <paramref name="messageId"/>, <paramref name="value"/>, <paramref name="accessCredentials"/>,
        /// <paramref name="service"/> or <paramref name="httpClient"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="value"/> has a grapheme length that does not equal 1.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
        public static async Task<AtProtoHttpResult<MessageView>> AddReaction(
            string conversationId,
            string messageId,
            string value,
            Uri service,
            AccessCredentials accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
            ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            ArgumentOutOfRangeException.ThrowIfNotEqual(value.GetGraphemeLength(), 1);

            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentNullException.ThrowIfNull(httpClient);

            AtProtoHttpClient<AddReactionResponse> client = new(ChatProxy, loggerFactory);

            AddReactionRequest request = new(conversationId, messageId, value);
            AtProtoHttpResult<AddReactionResponse> response = await client.Post(
                service,
                AddReactionEndpoint,
                request,
                credentials: accessCredentials,
                httpClient: httpClient,
                jsonSerializerOptions: BlueskyJsonSerializerOptions,
                onCredentialsUpdated: onCredentialsUpdated,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                return new AtProtoHttpResult<MessageView>(
                    response.Result.Message,
                    statusCode: response.StatusCode,
                    httpResponseHeaders: response.HttpResponseHeaders,
                    atErrorDetail: response.AtErrorDetail,
                    rateLimit: response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<MessageView>(
                    null,
                    statusCode: response.StatusCode,
                    httpResponseHeaders: response.HttpResponseHeaders,
                    atErrorDetail: response.AtErrorDetail,
                    rateLimit: response.RateLimit);
            }
        }

        /// <summary>
        /// Deletes the message specified by <paramref name="messageId"/> from the conversation identified by <paramref name="conversationId"/> for the authenticated user.
        /// </summary>
        /// <param name="conversationId">The conversation identifier to delete the message identified by <paramref name="messageId"/> from.</param>
        /// <param name="messageId">The message identifier to delete from <paramref name="conversationId"/>.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to delete the message from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> to use when accessing the <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="conversationId"/> or <paramref name="messageId"/> is whitespace.</exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when any of <paramref name="conversationId"/>, <paramref name="messageId"/>, <paramref name="accessCredentials"/>, <paramref name="service"/> or <paramref name="httpClient"/> are <see langword="null"/>.
        /// </exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
        public static async Task<AtProtoHttpResult<DeletedMessageView>> DeleteMessageForSelf(
            string conversationId,
            string messageId,
            Uri service,
            AccessCredentials accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
            ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentNullException.ThrowIfNull(httpClient);

            AtProtoHttpClient<DeletedMessageView> client = new(ChatProxy, loggerFactory);

            DeleteMessageRequest request = new(conversationId, messageId);

            AtProtoHttpResult<DeletedMessageView> response = await client.Post(
                service,
                DeleteMessageForSelfEndpoint,
                request,
                credentials: accessCredentials,
                httpClient: httpClient,
                jsonSerializerOptions : BlueskyJsonSerializerOptions,
                onCredentialsUpdated: onCredentialsUpdated,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            return response;
        }

        /// <summary>
        /// Gets a <see cref="ConversationView">view</see> over a conversation between <paramref name="members"/>.
        /// </summary>
        /// <param name="members">The <see cref="Did"/>s of the conversation members.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the conversation from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> to use when accessing the <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">
        ///   Thrown when <paramref name="members"/>, <paramref name="accessCredentials"/>, <paramref name="service"/> or <paramref name="httpClient"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="members"/> is empty or has greater than the maximum number of conversation members.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        public static async Task<AtProtoHttpResult<ConversationView>> GetConversationForMembers(
            ICollection<Did> members,
            Uri service,
            AccessCredentials accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(members);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentNullException.ThrowIfNull(httpClient);

            ArgumentOutOfRangeException.ThrowIfZero(members.Count);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(members.Count, Maximum.ConversationMembers);

            AtProtoHttpClient<ConversationResponse> client = new(ChatProxy, loggerFactory);

            StringBuilder queryStringBuilder = new();
            foreach (Did did in members)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"members={Uri.EscapeDataString(did)}&");
            }
            queryStringBuilder.Length--;

            string queryString = queryStringBuilder.ToString();

            AtProtoHttpResult<ConversationResponse> response = await client.Get(
                service,
                $"{GetConvoForMembersEndpoint}?{queryString}",
                credentials: accessCredentials,
                httpClient: httpClient,
                jsonSerializerOptions: BlueskyJsonSerializerOptions,
                onCredentialsUpdated: onCredentialsUpdated,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                return new AtProtoHttpResult<ConversationView>(
                    response.Result.Conversation,
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<ConversationView>(
                    null,
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
        }

        /// <summary>
        /// Enumerates a list of conversations the current user is a part of.
        /// </summary>
        /// <param name="limit">The number of conversations to return.</param>
        /// <param name="cursor">A cursor used for pagination.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the conversations from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> to use when accessing the <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when any of <paramref name="accessCredentials"/>, <paramref name="service"/> or <paramref name="httpClient"/> are <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/>is &lt;1 or &gt; the maximum number of conversations to list.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        public static async Task<AtProtoHttpResult<Conversations>> ListConversations(
            int? limit,
            string? cursor,
            Uri service,
            AccessCredentials accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentNullException.ThrowIfNull(httpClient);

            int limitValue = limit ?? 50;

            ArgumentOutOfRangeException.ThrowIfNegative(limitValue);
            ArgumentOutOfRangeException.ThrowIfZero(limitValue);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(limitValue, Maximum.ConversationsToList);

            StringBuilder queryStringBuilder = new();
            if (limit is not null)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&limit={limit}");
            }

            if (cursor is not null)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&cursor={Uri.EscapeDataString(cursor)}");
            }

            string queryString = queryStringBuilder.ToString();

            AtProtoHttpClient<ListConversationsResponse> client = new(ChatProxy, loggerFactory);

            AtProtoHttpResult<ListConversationsResponse> response = await client.Get(
                service,
                $"{ListConvosEndpoint}?{queryString}",
                credentials: accessCredentials,
                httpClient: httpClient,
                jsonSerializerOptions: BlueskyJsonSerializerOptions,
                onCredentialsUpdated: onCredentialsUpdated,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                return new AtProtoHttpResult<Conversations>(
                    new Conversations(response.Result.Conversations),
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<Conversations>(
                    null,
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
        }

        /// <summary>
        /// Gets a <see cref="ConversationView">view</see> over a conversation by its <paramref name="id"/>.
        /// </summary>
        /// <param name="id">The conversation identifier.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the conversation from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> to use when accessing the <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> is <see langword="null"/> or whitespace.</exception>
        /// <exception cref="ArgumentNullException">Thrown when any of <paramref name="accessCredentials"/>, <paramref name="service"/> or <paramref name="httpClient"/> are <see langword="null"/>.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        public static async Task<AtProtoHttpResult<ConversationView>> GetConversation(
            string id,
            Uri service,
            AccessCredentials accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(id);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentNullException.ThrowIfNull(httpClient);

            AtProtoHttpClient<ConversationResponse> client = new(ChatProxy, loggerFactory);

            AtProtoHttpResult<ConversationResponse> response = await client.Get(
                service,
                $"{GetConvoEndpoint}?convoId={Uri.EscapeDataString(id)}",
                credentials: accessCredentials,
                httpClient: httpClient,
                jsonSerializerOptions: BlueskyJsonSerializerOptions,
                onCredentialsUpdated: onCredentialsUpdated,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                return new AtProtoHttpResult<ConversationView>(
                    response.Result.Conversation,
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<ConversationView>(
                    null,
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
        }

        /// <summary>
        /// Enumerates the conversation log.
        /// </summary>
        /// <param name="cursor">An optional cursor used for pagination.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the conversation log from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> to use when accessing the <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="accessCredentials"/>, <paramref name="service"/> or <paramref name="httpClient"/> is <see langword="null"/>.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        public static async Task<AtProtoHttpResult<Logs>> GetConversationLog(
            string? cursor,
            Uri service,
            AccessCredentials accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentNullException.ThrowIfNull(httpClient);

            string queryString = string.Empty;
            if (cursor != null)
            {
                queryString = $"?cursor={Uri.EscapeDataString(cursor)}";
            }

            AtProtoHttpClient<GetLogResponse> client = new(ChatProxy, loggerFactory);

            AtProtoHttpResult<GetLogResponse> response = await client.Get(
                service,
                $"{GetLogEndpoint}{queryString}",
                credentials: accessCredentials,
                httpClient: httpClient,
                jsonSerializerOptions: BlueskyJsonSerializerOptions,
                onCredentialsUpdated: onCredentialsUpdated,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                return new AtProtoHttpResult<Logs>(
                    new Logs(response.Result.Logs, response.Result.Cursor),
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<Logs>(
                    null,
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
        }

        /// <summary>
        /// Enumerates the messages in a conversation.
        /// </summary>
        /// <param name="id">The conversation identifier whose messages should be retrieved.</param>
        /// <param name="limit">An optional limit on the number of messages to retrieve in each page.</param>
        /// <param name="cursor">An optional cursor used for pagination.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the conversation messages from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> to use when accessing the <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="id"/> is <see langword="null"/> or empty.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="accessCredentials"/>, <paramref name="service"/> or <paramref name="httpClient"/> is <see langword="null"/>.
        /// </exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        public static async Task<AtProtoHttpResult<Messages>> GetMessages(
            string id,
            int? limit,
            string? cursor,
            Uri service,
            AccessCredentials accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(id);

            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentNullException.ThrowIfNull(httpClient);

            if (limit is not null)
            {
                ArgumentOutOfRangeException.ThrowIfLessThan((int)limit, 1);
                ArgumentOutOfRangeException.ThrowIfGreaterThan((int)limit, Maximum.MessagesToList);
            }

            StringBuilder queryStringBuilder = new();
            queryStringBuilder.Append(CultureInfo.InvariantCulture, $"convoId={Uri.EscapeDataString(id)}");

            if (cursor != null)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&cursor={Uri.EscapeDataString(cursor)}");
            }

            if (limit != null)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&limit={limit}");
            }

            string queryString = queryStringBuilder.ToString();

            AtProtoHttpClient<GetMessagesResponse> client = new(ChatProxy, loggerFactory);

            AtProtoHttpResult<GetMessagesResponse> response = await client.Get(
                service,
                $"{GetMessagesEndpoint}?{queryString}",
                credentials: accessCredentials,
                httpClient: httpClient,
                jsonSerializerOptions: BlueskyJsonSerializerOptions,
                onCredentialsUpdated: onCredentialsUpdated,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                return new AtProtoHttpResult<Messages>(
                    new Messages(response.Result.Messages, response.Result.Cursor),
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<Messages>(
                    null,
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
        }

        /// <summary>
        /// Leaves the conversation identified by <paramref name="id"/>.
        /// </summary>
        /// <param name="id">The conversation identifier to leave.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to leave the conversation on.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> to use when accessing the <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="id"/> is <see langword="null"/> or empty.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="accessCredentials"/>, <paramref name="service"/> or <paramref name="httpClient"/> is <see langword="null"/>.
        /// </exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
        public static async Task<AtProtoHttpResult<ConversationReference>> LeaveConversation(
            string id,
            Uri service,
            AccessCredentials accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(id);

            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentNullException.ThrowIfNull(httpClient);

            AtProtoHttpClient<ConversationReference> client = new(ChatProxy, loggerFactory);

            ConversationIdPostRequest request = new(id);

            AtProtoHttpResult<ConversationReference> response = await client.Post(
                service,
                $"{LeaveConvoEndpoint}",
                request,
                credentials: accessCredentials,
                httpClient:httpClient,
                jsonSerializerOptions: BlueskyJsonSerializerOptions,
                onCredentialsUpdated: onCredentialsUpdated,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                return response;
            }
            else
            {
                return new AtProtoHttpResult<ConversationReference>(
                    null,
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
        }

        /// <summary>
        /// Mutes the conversation identified by <paramref name="id"/>.
        /// </summary>
        /// <param name="id">The conversation identifier to mute.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to mute the conversation on.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> to use when accessing the <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="id"/> is <see langword="null"/> or empty.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="accessCredentials"/>, <paramref name="service"/> or <paramref name="httpClient"/> is <see langword="null"/>.
        /// </exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
        public static async Task<AtProtoHttpResult<ConversationView>> MuteConversation(
            string id,
            Uri service,
            AccessCredentials accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(id);

            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentNullException.ThrowIfNull(httpClient);

            AtProtoHttpClient<ConversationResponse> client = new(ChatProxy, loggerFactory);

            ConversationIdPostRequest request = new(id);

            AtProtoHttpResult<ConversationResponse> response = await client.Post(
                service,
                $"{MuteConvoEndpoint}",
                request,
                credentials: accessCredentials,
                httpClient: httpClient,
                jsonSerializerOptions: BlueskyJsonSerializerOptions,
                onCredentialsUpdated: onCredentialsUpdated,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                return new AtProtoHttpResult<ConversationView>(
                    response.Result.Conversation,
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<ConversationView>(
                    null,
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
        }

        /// <summary>
        /// Removes the specified reaction to the message identified by <paramref name="messageId"/> from the conversation identified by <paramref name="conversationId"/> for the authenticated user.
        /// </summary>
        /// <param name="conversationId">The conversation identifier to remove the reaction to identified by <paramref name="messageId"/> from.</param>
        /// <param name="messageId">The message identifier to remove the reaction to from <paramref name="conversationId"/>.</param>
        /// <param name="value">The reaction to remove.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to delete the message from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> to use when accessing the <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="conversationId"/>, <paramref name="messageId"/> or <paramref name="value"/> is whitespace.</exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="conversationId"/>, <paramref name="messageId"/>, <paramref name="value"/>, <paramref name="accessCredentials"/>, <paramref name="service"/> or <paramref name="httpClient"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="value"/> has a grapheme length that does not equal 1.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
        public static async Task<AtProtoHttpResult<MessageView>> RemoveReaction(
            string conversationId,
            string messageId,
            string value,
            Uri service,
            AccessCredentials accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
            ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            ArgumentOutOfRangeException.ThrowIfNotEqual(value.GetGraphemeLength(), 1);

            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentNullException.ThrowIfNull(httpClient);

            AtProtoHttpClient<RemoveReactionResponse> client = new(ChatProxy, loggerFactory);

            RemoveReactionRequest request = new(conversationId, messageId, value);
            AtProtoHttpResult<RemoveReactionResponse> response = await client.Post(
                service,
                RemoveReactionEndpoint,
                request,
                credentials: accessCredentials,
                httpClient: httpClient,
                jsonSerializerOptions: BlueskyJsonSerializerOptions,
                onCredentialsUpdated: onCredentialsUpdated,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                return new AtProtoHttpResult<MessageView>(
                    response.Result.Message,
                    statusCode: response.StatusCode,
                    httpResponseHeaders: response.HttpResponseHeaders,
                    atErrorDetail: response.AtErrorDetail,
                    rateLimit: response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<MessageView>(
                    null,
                    statusCode: response.StatusCode,
                    httpResponseHeaders: response.HttpResponseHeaders,
                    atErrorDetail: response.AtErrorDetail,
                    rateLimit: response.RateLimit);
            }
        }

        /// <summary>
        /// Sends the specified <paramref name="batchedMessages"/>.
        /// </summary>
        /// <param name="batchedMessages">The collection of <see cref="BatchedMessage"/>s to send.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to unmute the conversation on.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> to use when accessing the <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="batchedMessages"/>, <paramref name="accessCredentials"/>, <paramref name="service"/> or <paramref name="httpClient"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="batchedMessages"/> is empty or has greater than the maximum allowed number of messages.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
        public static async Task<AtProtoHttpResult<ICollection<MessageView>>> SendMessageBatch(
            ICollection<BatchedMessage> batchedMessages,
            Uri service,
            AccessCredentials accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(batchedMessages);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentNullException.ThrowIfNull(httpClient);

            ArgumentOutOfRangeException.ThrowIfZero(batchedMessages.Count);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(batchedMessages.Count, Maximum.BatchedMessages);

            AtProtoHttpClient<SendMessageBatchResponse> client = new(ChatProxy, loggerFactory);

            SendMessageBatchRequest request = new(batchedMessages);

            AtProtoHttpResult<SendMessageBatchResponse> response = await client.Post(
                service,
                $"{SendMessageBatchEndpoint}",
                request,
                credentials: accessCredentials,
                httpClient: httpClient,
                jsonSerializerOptions: BlueskyJsonSerializerOptions,
                onCredentialsUpdated: onCredentialsUpdated,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                return new AtProtoHttpResult<ICollection<MessageView>>(
                    response.Result.Items,
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<ICollection<MessageView>>(
                    null,
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
        }

        /// <summary>
        /// Sends the specified <see cref="MessageInput"/> to the conversation identified by <paramref name="id"/>.
        /// </summary>
        /// <param name="id">The conversation identifier to send the <paramref name="message"/> to.</param>
        /// <param name="message">The <see cref="MessageInput"/> to send.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to unmute the conversation on.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> to use when accessing the <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="id"/> is <see langword="null"/> or empty.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="message"/>, <paramref name="accessCredentials"/>, <paramref name="service"/> or <paramref name="httpClient"/> is <see langword="null"/>.
        /// </exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
        public static async Task<AtProtoHttpResult<MessageView>> SendMessage(
            string id,
            MessageInput message,
            Uri service,
            AccessCredentials accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(id);

            ArgumentNullException.ThrowIfNull(message);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentNullException.ThrowIfNull(httpClient);

            AtProtoHttpClient<MessageView> client = new(ChatProxy, loggerFactory);

            SendMessageRequest request = new(id, message);

            AtProtoHttpResult<MessageView> response = await client.Post(
                service,
                $"{SendMessageEndpoint}",
                request,
                credentials: accessCredentials,
                httpClient: httpClient,
                jsonSerializerOptions: BlueskyJsonSerializerOptions,
                onCredentialsUpdated: onCredentialsUpdated,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                return response;
            }
            else
            {
                return new AtProtoHttpResult<MessageView>(
                    null,
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
        }

        /// <summary>
        /// Unmutes the conversation identified by <paramref name="id"/>.
        /// </summary>
        /// <param name="id">The conversation identifier to unmute.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to unmute the conversation on.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> to use when accessing the <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="id"/> is <see langword="null"/> or empty.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="accessCredentials"/>, <paramref name="service"/> or <paramref name="httpClient"/> is <see langword="null"/>.
        /// </exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
        public static async Task<AtProtoHttpResult<ConversationView>> UnmuteConversation(
            string id,
            Uri service,
            AccessCredentials accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(id);

            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentNullException.ThrowIfNull(httpClient);

            AtProtoHttpClient<ConversationResponse> client = new(ChatProxy, loggerFactory);

            ConversationIdPostRequest request = new(id);

            AtProtoHttpResult<ConversationResponse> response = await client.Post(
                service,
                $"{UnmuteConvoEndpoint}",
                request,
                credentials: accessCredentials,
                httpClient: httpClient,
                jsonSerializerOptions: BlueskyJsonSerializerOptions,
                onCredentialsUpdated: onCredentialsUpdated,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                return new AtProtoHttpResult<ConversationView>(
                    response.Result.Conversation,
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<ConversationView>(
                    null,
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
        }

        /// <summary>
        /// Marks all conversations with the specified <paramref name="status"/> as read.
        /// </summary>
        /// <param name="status">The <see cref="ConversationStatus"/> of the conversations to mark as read.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to mark the conversation on.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> to use when accessing the <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="status"/>, <paramref name="accessCredentials"/>, <paramref name="service"/> or <paramref name="httpClient"/> is <see langword="null"/>.
        /// </exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
        public static async Task<AtProtoHttpResult<ulong>> UpdateAllRead(
            ConversationStatus status,
            Uri service,
            AccessCredentials accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentNullException.ThrowIfNull(httpClient);

            AtProtoHttpClient<UpdateAllReadResponse> client = new(ChatProxy, loggerFactory);

            UpdateAllReadRequest request = new(status);

            AtProtoHttpResult<UpdateAllReadResponse> response = await client.Post(
                service,
                $"{UpdateAllReadEndpoint}",
                request,
                credentials: accessCredentials,
                httpClient: httpClient,
                jsonSerializerOptions: BlueskyJsonSerializerOptions,
                onCredentialsUpdated: onCredentialsUpdated,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                return new AtProtoHttpResult<ulong>(
                    response.Result.UpdatedCount,
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<ulong>(
                    0,
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
        }

        /// <summary>
        /// Marks a conversation, and optionally a message, as read.
        /// </summary>
        /// <param name="conversationId">The conversation identifier to mark as read.</param>
        /// <param name="messageId">The message identifier in the conversation identified by <paramref name="conversationId"/> to mark as read.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to mark the conversation on.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> to use when accessing the <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="conversationId"/> is <see langword="null"/> or whitespace, <paramref name="messageId"/> is empty or whitespace,
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="accessCredentials"/>, <paramref name="service"/> or <paramref name="httpClient"/> is <see langword="null"/>.
        /// </exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
        public static async Task<AtProtoHttpResult<ConversationView>> UpdateRead(
            string conversationId,
            string? messageId,
            Uri service,
            AccessCredentials accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);

            if (messageId is not null)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
            }

            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentNullException.ThrowIfNull(httpClient);

            AtProtoHttpClient<ConversationResponse> client = new(ChatProxy, loggerFactory);

            UpdateReadRequest request = new(conversationId, messageId);

            AtProtoHttpResult<ConversationResponse> response = await client.Post(
                service,
                $"{UpdateReadEndpoint}",
                request,
                credentials: accessCredentials,
                httpClient: httpClient,
                jsonSerializerOptions: BlueskyJsonSerializerOptions,
                onCredentialsUpdated: onCredentialsUpdated,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                return new AtProtoHttpResult<ConversationView>(
                    response.Result.Conversation,
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<ConversationView>(
                    null,
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
        }
    }
}
