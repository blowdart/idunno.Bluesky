// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Chat;
using idunno.Bluesky.Chat.Convo.Model;

using Microsoft.Extensions.Logging;

namespace idunno.Bluesky;

public static partial class BlueskyServer
{
    /// <summary>
    /// Adds a reaction to the message identified by <paramref name="messageId"/> from the conversation identified by <paramref name="conversationId"/> for the authenticated user.
    /// </summary>
    /// <param name="conversationId">The conversation identifier to add the reaction to identified by <paramref name="messageId"/> from.</param>
    /// <param name="messageId">The message identifier to add the reaction to from <paramref name="conversationId"/>.</param>
    /// <param name="value">The reaction to add.</param>
    /// <param name="service">The <see cref="Uri"/> of the service to add the reaction on.</param>
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

        BlueskyHttpClient<AddReactionResponse> client = new(ChatProxy, loggerFactory);

        AddReactionRequest request = new(conversationId, messageId, value);
        AtProtoHttpResult<AddReactionResponse> response = await client.Post(
            service,
            "/xrpc/chat.bsky.convo.addReaction",
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
}