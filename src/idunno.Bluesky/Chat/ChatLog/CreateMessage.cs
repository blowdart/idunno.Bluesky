// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

#pragma warning disable IDE0130
namespace idunno.Bluesky.Chat
#pragma warning restore IDE0130
{
    /// <summary>
    /// A log entry indicating a message was created in a chat.
    /// </summary>
    public sealed record CreateMessage : MessageLogBase
    {
        /// <summary>
        /// Constructs a new instance of <see cref="CreateMessage"/>.
        /// </summary>
        /// <param name="id">The conversation identifier.</param>
        /// <param name="revision">The conversation revision.</param>
        /// <param name="message">A <see cref="MessageViewBase">view</see> over the message the log entry refers to.</param>
        internal CreateMessage(string id, string revision, MessageViewBase message) : base(id, revision, message)
        {
        }
    }
}
