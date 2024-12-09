// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

#pragma warning disable IDE0130
namespace idunno.Bluesky.Chat
#pragma warning restore IDE0130

{
    /// <summary>
    /// Base record for chat log message entries.
    /// </summary>
    [JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = true, UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToNearestAncestor)]
    public record MessageLogBase : LogBase
    {
        /// <summary>
        /// Creates a new instance of <see cref="LogBase"/>.
        /// </summary>
        /// <param name="id">The conversation identifier.</param>
        /// <param name="revision">The conversation revision.</param>
        /// <param name="message">A <see cref="MessageViewBase">view</see> over the message the log entry refers to.</param>
        protected MessageLogBase(string id, string revision, MessageViewBase message) : base(id, revision)
        {
            Message = message;
        }

        /// <summary>
        /// Gets the <see cref="MessageViewBase">view</see> over the message the log entry refers to
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public MessageViewBase Message { get; init; }
    }
}
