// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

#pragma warning disable IDE0130
namespace idunno.Bluesky.Chat
#pragma warning restore IDE0130
{
    /// <summary>
    /// A log entry indicating the start of a conversation.
    /// </summary>
    public sealed record BeginConversation : LogBase
    {
        /// <summary>
        /// Constructs a new instance of <see cref="BeginConversation"/>.
        /// </summary>
        /// <param name="id">The conversation identifier.</param>
        /// <param name="revision">The conversation revision.</param>
        internal BeginConversation(string id, string revision) : base(id, revision)
        {
        }
    }
}
