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
        /// <param name="facets">Facets to apply to the message <paramref name="text"/>, if any.</param>
        /// <param name="embed">A view over the embedded record in the message, if any.</param>
        /// <param name="reactions">Reactions to the message <paramref name="text"/>, if any.</param>
        /// <param name="sender">A view over the message author.</param>
        /// <param name="sentAt">The <see cref="DateTimeOffset"/> the message was sent on.</param>
        /// <exception cref="ArgumentException">
        ///   Thrown when <paramref name="id" /> or <paramref name="revision"/> is null or whitespace.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        ///   Thrown when <paramref name="text"/> or <paramref name="sentAt"/> or <paramref name="sender"/> is null.
        /// </exception>
        public MessageView(
            string id,
            string revision,
            string text,
            IReadOnlyCollection<Facet>? facets,
            EmbeddedRecordView embed,
            IReadOnlyCollection<ReactionView>? reactions,
            MessageViewSender sender,
            DateTimeOffset sentAt) : base(id, revision, sender, sentAt)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(id);
            ArgumentException.ThrowIfNullOrWhiteSpace(revision);

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

            if (reactions == null)
            {
                Reactions = new List<ReactionView>().AsReadOnly();
            }
            else
            {
                Reactions = new List<ReactionView>(reactions).AsReadOnly();
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
        public IReadOnlyCollection<Facet> Facets { get; init; }

        /// <summary>
        /// Gets a view over the embedded record, if any.
        /// </summary>
        [JsonInclude]
        public EmbeddedRecordView? Embed { get; init; }

        /// <summary>
        /// Gets reactions to the message, in ascending order of creation time.
        /// </summary>
        [JsonInclude]
        public IReadOnlyCollection<ReactionView> Reactions { get; init; }
    }
}
