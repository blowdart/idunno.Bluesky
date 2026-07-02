// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Chat;
using idunno.Bluesky.RichText;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Sends the specified <see cref="MessageInput"/> to the conversation identified by <paramref name="conversationId"/>.
    /// </summary>
    /// <param name="conversationId">The conversation identifier to send the <paramref name="message"/> to.</param>
    /// <param name="message">The message to send.</param>
    /// <param name="extractFacets">Flag indicating whether facets should be extracted from <paramref name="message" />.</param>
    /// <param name="embeddedPost">A <see cref="StrongReference"/> to a post that will be embedded in the message.</param>
    /// <param name="replyTo">A <see cref="ReplyReference"/> to a message in the same conversation that this message is replying to.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="conversationId"/> is <see langword="null"/> or white space.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="message"/> is <see langword="null"/>, or if <paramref name="embeddedPost"/> is specified but its collection is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="embeddedPost"/> is specified but it is not in the <see cref="CollectionNsid.Post"/> collection.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<MessageView>> SendMessage(
        string conversationId,
        string message,
        bool extractFacets = true,
        StrongReference? embeddedPost = null,
        ReplyReference? replyTo= null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);

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

        messageInput.ReplyTo = replyTo;

        return await BlueskyServer.SendMessage(
            conversationId,
            messageInput,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            cancellationToken: cancellationToken).ConfigureAwait(false);

    }
}
