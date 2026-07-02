// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.Bluesky.Actor;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Chat;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Presents a view over a conversation.
/// </summary>
public sealed record ConversationView : ConversationViewBase
{
    /// <summary>
    /// Creates a new instance of <see cref="ConversationView"/>.
    /// </summary>
    /// <param name="id">The conversation identifier.</param>
    /// <param name="revision">The conversation revision.</param>
    /// <param name="members">The <see cref="ProfileViewBasic"/> of the members of the conversation.</param>
    /// <param name="lastMessage">A view over the last message in the conversation, if any.</param>
    /// <param name="lastReaction">A view of the last message and reaction to it in a conversation, if any.</param>
    /// <param name="muted">A flag indicating whether the conversation is muted.</param>
    /// <param name="unreadCount">A count of the number of unread messages in the conversation.</param>
    /// <param name="status">The status of the conversation. If <see langword="null"/> defaults to <see cref="ConversationStatus.Requested"/></param>
    /// <param name="kind">The kind of conversation, if any.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="id"/>, <paramref name="revision"/> is <see langword="null"/> or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="members"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="members"/> is empty.</exception>
    [JsonConstructor]
    public ConversationView(
        string id,
        string revision,
        IReadOnlyCollection<Actor.ProfileViewBasic> members,
        MessageViewBase? lastMessage,
        MessageAndReactionView? lastReaction,
        bool muted,
        long unreadCount,
        string? status,
        ConversationKind? kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(revision);

        ArgumentNullException.ThrowIfNull(members);

        ArgumentOutOfRangeException.ThrowIfZero(members.Count);

        Id = id;
        Revision = revision;
        Members = new List<Actor.ProfileViewBasic>(members).AsReadOnly();
        LastMessage = lastMessage;
        LastReaction = lastReaction;
        Muted = muted;
        UnreadCount = unreadCount;
        Status = status;
        Kind = kind;
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
    public IReadOnlyCollection<Actor.ProfileViewBasic> Members { get; init; }

    /// <summary>
    /// Gets a <see cref="MessageViewBase">view</see> over the last message in the conversation, if any.
    /// </summary>
    [JsonInclude]
    public MessageViewBase? LastMessage { get; init; }

    /// <summary>
    /// Gets a <see cref="MessageAndReactionView"/> of the last reaction to a conversation, if any.
    /// </summary>
    [JsonInclude]
    public MessageAndReactionView? LastReaction { get; init; }

    /// <summary>
    /// Gets a flag indicating whether the conversation is muted.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public bool Muted { get; init; }

    /// <summary>
    /// Gets a count of the number of unread messages in the conversation.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public long UnreadCount { get; init; }

    /// <summary>
    /// Gets the status of the conversation. Known values are defined in <see cref="ConversationStatus"/>.
    /// </summary>
    [JsonInclude]
    public string? Status { get; init; }

    /// <summary>
    /// Gtets the kind of conversation, if any.
    /// </summary>
    public ConversationKind? Kind { get; init; }
}