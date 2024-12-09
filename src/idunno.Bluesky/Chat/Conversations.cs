// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Chat
{
    /// <summary>
    /// A paged readonly collection of <see cref="ConversationView"/>s.
    /// </summary>
    public class Conversations : PagedViewReadOnlyCollection<ConversationView>
    {
        /// <summary>
        /// Creates a new instance of <see cref="Conversations"/>.
        /// </summary>
        public Conversations() : base(new List<ConversationView>(), null)
        {
        }

        /// <summary>
        /// Creates a new instance of <see cref="Conversations"/>.
        /// </summary>
        /// <param name="list">The list of <see cref="ConversationView"/> to create this instance of <see cref="Conversations"/> from.</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        public Conversations(IList<ConversationView> list, string? cursor = null) : base(list, cursor)
        {
        }

        /// <summary>
        /// Creates a new instance of <see cref="Conversations"/>.
        /// </summary>
        /// <param name="collection">A collection of <see cref="ConversationView"/> to create this instance of <see cref="Conversations"/> from.</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        public Conversations(ICollection<ConversationView> collection, string? cursor = null) : this(new List<ConversationView>(collection), cursor)
        {
        }
    }
}
