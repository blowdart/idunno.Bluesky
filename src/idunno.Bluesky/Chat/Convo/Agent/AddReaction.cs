// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text;

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
    /// Adds a reaction to the message identified by <paramref name="messageId"/> from the conversation identified by <paramref name="conversationId"/> for the authenticated user.
    /// </summary>
    /// <param name="conversationId">The conversation identifier to add the reaction to identified by <paramref name="messageId"/> from.</param>
    /// <param name="messageId">The message identifier to add the reaction to from <paramref name="conversationId"/>.</param>
    /// <param name="value">The reaction to add.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when any of <paramref name="conversationId"/>, <paramref name="messageId"/> or <paramref name="value"/> are <see langword="null"/> or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="value"/> has a grapheme length that does not equal 1, or is longer than <see cref="Maximum.ReactionLengthInBytes"/> UTF-8 bytes.</exception>
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

        // A single grapheme cluster has no upper bound on its encoded length, so the lexicon's maxLength is checked separately
        // rather than being implied by the grapheme check above.
        ArgumentOutOfRangeException.ThrowIfGreaterThan(Encoding.UTF8.GetByteCount(value), Maximum.ReactionLengthInBytes);

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
            maximumResponseSize: MaximumResponseSize,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}