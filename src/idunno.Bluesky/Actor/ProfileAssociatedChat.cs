// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Actor
{
    /// <summary>
    /// Encapsulates the chat configuration for an actor.
    /// </summary>
    public record ProfileAssociatedChat
    {
        /// <summary>
        /// Creates an instance of <see cref="ProfileAssociatedChat"/>
        /// </summary>
        /// <param name="allowIncoming"></param>
        public ProfileAssociatedChat(AllowIncomingChat allowIncoming) => AllowIncoming = allowIncoming;

        /// <summary>
        /// Gets a flag indicating the rules for incoming chats.
        /// </summary>
        public AllowIncomingChat AllowIncoming { get; init; }
    }

    /// <summary>
    /// Configuration values for an actor's incoming chat configuration.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<AllowIncomingChat>))]
    public enum AllowIncomingChat
    {
        /// <summary>
        /// No incoming chats are allowed.
        /// </summary>
        None = 0,

        /// <summary>
        /// Chats from anyone are allowed.
        /// </summary>
        All = 1,

        /// <summary>
        /// Chats are only allowed from the actor's followers.
        /// </summary>
        Following = 2
    }
}
