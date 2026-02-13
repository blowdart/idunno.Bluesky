// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Chat
{
    /// <summary>
    /// Presents a view over a message that has been deleted.
    /// </summary>
    public sealed record DeletedMessageView : MessageViewBase
    {
        /// <summary>
        /// Creates a new instance of <see cref="DeletedMessageView"/>.
        /// </summary>
        /// <param name="id">The message ID.</param>
        /// <param name="revision">The message revision.</param>
        /// <param name="sender">A view over the message author.</param>
        /// <param name="sentAt">The <see cref="DateTimeOffset"/> the message was sent on.</param>
        /// <exception cref="ArgumentException">
        ///   Thrown when <paramref name="id" /> or <paramref name="revision"/> is <see langword="null"/> or whitespace.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        ///   Thrown when <paramref name="sender"/> is <see langword="null"/>.
        /// </exception>
        public DeletedMessageView(
            string id,
            string revision,
            MessageViewSender sender,
            DateTimeOffset sentAt) : base(id, revision, sender, sentAt)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(id);
            ArgumentException.ThrowIfNullOrWhiteSpace(revision);

            ArgumentNullException.ThrowIfNull(sender);
        }
    }
}
