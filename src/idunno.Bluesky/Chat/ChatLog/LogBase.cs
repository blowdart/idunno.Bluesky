// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto.Repo;

#pragma warning disable IDE0130
namespace idunno.Bluesky.Chat
#pragma warning restore IDE0130
{
    /// <summary>
    /// Base record for log entries.
    /// </summary>
    [JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = true, UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToNearestAncestor)]
    [JsonDerivedType(typeof(BeginConversation), typeDiscriminator: ConversationLogTypeDiscriminators.BeginConversation)]
    [JsonDerivedType(typeof(LeaveConversation), typeDiscriminator: ConversationLogTypeDiscriminators.LeaveConversation)]
    [JsonDerivedType(typeof(CreateMessage), typeDiscriminator: ConversationLogTypeDiscriminators.CreateMessage)]
    [JsonDerivedType(typeof(DeleteMessage), typeDiscriminator: ConversationLogTypeDiscriminators.DeleteMessage)]
    public record LogBase : AtProtoObject
    {
        /// <summary>
        /// Creates a new instance of <see cref="LogBase"/>.
        /// </summary>
        /// <param name="id">The conversation identifier.</param>
        /// <param name="revision">The conversation revision.</param>
        protected LogBase(string id, string revision)
        {
            Id = id;
            Revision = revision;
        }

        /// <summary>
        /// Gets the conversation identifier.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        [JsonPropertyName("convoId")]
        public string Id { get; init; }

        /// <summary>
        /// Gets the conversation revision.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        [JsonPropertyName("rev")]
        public string Revision { get; init; }
    }
}
