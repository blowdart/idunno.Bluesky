// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.Bluesky.Actor;

namespace idunno.Bluesky.Chat
{
    /// <summary>
    /// Presents a view over a conversation.
    /// </summary>
    public sealed record ConversationView : View
    {
        /// <summary>
        /// Creates a new instance of <see cref="ConversationView"/>.
        /// </summary>
        /// <param name="id">The conversation identifier.</param>
        /// <param name="revision">The conversation revision.</param>
        /// <param name="members">The <see cref="ProfileViewBasic"/> of the members of the conversation.</param>
        /// <param name="lastMessage">A view over the last message in the conversation, if any.</param>
        /// <param name="muted">A flag indicating whether the conversation is muted.</param>
        /// <param name="opened">A flag indicating whether the conversation has been opened.</param>
        /// <param name="unreadCount">A count of the number of unread messages in the conversation.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="id"/>, <paramref name="revision"/> or <paramref name="members"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="members"/> is empty.</exception>
        public ConversationView(
            string id,
            string revision,
            IReadOnlyCollection<ProfileViewBasic> members,
            MessageViewBase? lastMessage,
            bool muted,
            bool opened,
            long unreadCount)
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(id);
            ArgumentNullException.ThrowIfNullOrWhiteSpace(revision);
            ArgumentNullException.ThrowIfNull(members);
            ArgumentOutOfRangeException.ThrowIfZero(members.Count);

            Id = id;
            Revision = revision;
            Members = new List<ProfileViewBasic>(members).AsReadOnly();
            LastMessage = lastMessage;
            Muted = muted;
            Opened = opened;
            UnreadCount = unreadCount;
        }

        /// <summary>
        /// Gets the conversation identifier.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public string Id { get; init; }

        /// <summary>
        /// Gets the conversation revision.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        [JsonPropertyName("rev")]
        public string Revision { get; init; }

        /// <summary>
        /// Gets the <see cref="ProfileViewBasic"/> of the members of the conversation.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public IReadOnlyCollection<ProfileViewBasic> Members { get; init; }

        /// <summary>
        /// Gets a <see cref="MessageViewBase">view</see> over the last message in the conversation, if any.
        /// </summary>
        [JsonInclude]
        public MessageViewBase? LastMessage { get; init; }

        /// <summary>
        /// Gets a flag indicating whether the conversation is muted.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public bool Muted { get; init; }

        /// <summary>
        /// Gets a flag indicating whether the conversation has been opened.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public bool Opened { get; init; }

        /// <summary>
        /// Gets a count of the number of unread messages in the conversation.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public long UnreadCount { get; init; }
    }
}
