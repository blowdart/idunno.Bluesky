// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using idunno.Bluesky.Embed;
using idunno.Bluesky.RichText;

namespace idunno.Bluesky.Chat
{
    /// <summary>
    /// Presents a view over a message.
    /// </summary>
    public sealed record MessageView : MessageViewBase
    {
        /// <summary>
        /// Creates a new instance of <see cref="MessageView"/>.
        /// </summary>
        /// <param name="id">The message ID.</param>
        /// <param name="revision">The message revision.</param>
        /// <param name="text">The text of the message.</param>
        /// <param name="facets">Any facets to apply to the message <paramref name="text"/>.</param>
        /// <param name="embed">A view over the embedded record in the message, if any.</param>
        /// <param name="sender">A view over the message author.</param>
        /// <param name="sentAt">The <see cref="DateTimeOffset"/> the message was sent on.</param>
        /// <exception cref="ArgumentNullException">
        ///   Thrown when <paramref name="text"/> or <paramref name="sentAt"/> or <paramref name="sender"/> is null, or
        ///   when <paramref name="id" /> or <paramref name="revision"/> is null or whitespace.
        /// </exception>
        public MessageView(
            string id,
            string revision,
            string text,
            IReadOnlyCollection<Facet> facets,
            MessageViewSender sender,
            EmbeddedRecordView embed,
            DateTimeOffset sentAt) : base(id, revision, sender, sentAt)
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(id);
            ArgumentNullException.ThrowIfNullOrWhiteSpace(revision);
            ArgumentNullException.ThrowIfNull(text);
            ArgumentNullException.ThrowIfNull(sender);
            ArgumentNullException.ThrowIfNull(sentAt);

            Text = text;
            Embed = embed;

            if (facets == null)
            {
                Facets = new List<Facet>().AsReadOnly();
            }
            else
            {
                Facets = new List<Facet>(facets).AsReadOnly();
            }
        }

        /// <summary>
        /// Gets the text of a messages.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public string Text { get; init; }

        /// <summary>
        /// Gets any facets to apply to <see cref="Text"/>.
        /// </summary>
        [JsonInclude]
        [NotNull]
        public IReadOnlyCollection<Facet>? Facets { get; init; }

        /// <summary>
        /// Gets a view over the embedded record, if any.
        /// </summary>
        [JsonInclude]
        public EmbeddedRecordView? Embed { get; init; }
    }
}
