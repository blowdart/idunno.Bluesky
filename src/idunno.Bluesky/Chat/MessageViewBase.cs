// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Chat
{
    /// <summary>
    /// Base class for message views. Used for json polymorphic deserialization.
    /// </summary>
    [JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = true, UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToNearestAncestor)]
    [JsonDerivedType(typeof(MessageView), typeDiscriminator: MessageTypeDiscriminators.MessageView)]
    [JsonDerivedType(typeof(DeletedMessageView), typeDiscriminator: MessageTypeDiscriminators.DeletedMessageView)]
    public record MessageViewBase : View
    {
        /// <summary>
        /// Constructs a new instance of <see cref="MessageViewBase"/>.
        /// </summary>
        /// <param name="id">The message ID.</param>
        /// <param name="revision">The message revision.</param>
        /// <param name="sender">A view over the message author.</param>
        /// <param name="sentAt">The <see cref="DateTimeOffset"/> the message was sent on.</param>
        /// <exception cref="ArgumentNullException">
        ///   Thrown when <paramref name="sentAt"/> or <paramref name="sender"/> is null, or
        ///   when <paramref name="id" /> or <paramref name="revision"/> is null or whitespace.
        /// </exception>
        [JsonConstructor]
        public MessageViewBase(string id, string revision, MessageViewSender sender, DateTimeOffset sentAt)
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(id);
            ArgumentNullException.ThrowIfNullOrWhiteSpace(revision);
            ArgumentNullException.ThrowIfNull(sender);
            ArgumentNullException.ThrowIfNull(sentAt);

            Id = id;
            Revision = revision;
            Sender = sender;
            SentAt = sentAt;
        }

        /// <summary>
        /// Gets the id of a message.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public string Id { get; init; }

        /// <summary>
        /// Gets the revision of a message.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        [JsonPropertyName("rev")]
        public string Revision { get; init; }

        /// <summary>
        /// Gets a view over the message author.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public MessageViewSender Sender { get; init; }

        /// <summary>
        /// Gets the <see cref="DateTimeOffset"/> the message was sent on.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public DateTimeOffset SentAt { get; init; }
    }
}
