// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Chat;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Encapsulates a reference to a conversation and its revision.
/// </summary>
public record ConversationReference
{
    [JsonConstructor]
    internal ConversationReference(string conversationId, string revision)
    {
        ArgumentNullException.ThrowIfNull(conversationId);
        ArgumentNullException.ThrowIfNull(revision);

        ConversationId = conversationId;
        Revision = revision;
    }

    /// <summary>
    /// Gets the conversation identifier.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    [JsonPropertyName("convoId")]
    public string ConversationId { get; init; }

    /// <summary>
    /// Gets the conversation revision.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    [JsonPropertyName("rev")]
    public string Revision { get; init; }
}