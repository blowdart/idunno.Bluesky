// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Chat;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Encapsulates a reference to an individual message in a conversation.
/// </summary>
public sealed record MessageReference
{
    /// <summary>
    /// Creates a new instance of <see cref="MessageReference"/>.
    /// </summary>
    /// <param name="did">The <see cref="AtProto.Did"/> of the message author.</param>
    /// <param name="conversationId">The conversation identifier.</param>
    /// <param name="messageId">The message identifier.</param>
    /// <exception cref="ArgumentNullException"><paramref name="did"/>, <paramref name="conversationId"/> or <paramref name="messageId"/> is <see langword="null"/>.</exception>
    [JsonConstructor]
    public MessageReference(Did did, string conversationId, string messageId)
    {
        ArgumentNullException.ThrowIfNull(did);

        ArgumentNullException.ThrowIfNull(conversationId);
        ArgumentNullException.ThrowIfNull(messageId);

        Did = did;
        ConversationId = conversationId;
        MessageId = messageId;
    }

    /// <summary>
    /// Gets the <see cref="AtProto.Did"/> of the message author.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public Did Did { get; init; }

    /// <summary>
    /// Gets the conversation identifier.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public string ConversationId { get; init; }

    /// <summary>
    /// Gets the message identifier.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public string MessageId { get; init; }
}