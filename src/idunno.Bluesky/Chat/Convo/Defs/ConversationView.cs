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
    /// <param name="status">The status of the conversation. Known values are defined in <see cref="ConversationStatus"/>.</param>
    /// <param name="kind">The kind of conversation, if any.</param>
    /// <exception cref="ArgumentNullException"><paramref name="id"/>, <paramref name="revision"/> or <paramref name="members"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="members"/> contains a <see langword="null"/> entry.</exception>
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
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(revision);

        ArgumentNullException.ThrowIfNull(members);

        Id = id;
        Revision = revision;
        Members = members;
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
    /// For group convos, it will be a collection of important members (the first few members, the viewer,
    /// the member who added the viewer, the member who sent the last message, the member who sent the last reaction),
    /// but will not contain the full list of members. Use <see cref="BlueskyAgent.GetConversationMembers(string, int?, string?, CancellationToken)"/> to list all members.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when the value set is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The collection contains a <see langword="null"/> entry.</exception>
    /// <remarks>
    /// <para>
    /// The value supplied is copied, so later changes to the collection assigned are not reflected here.
    /// </para>
    /// </remarks>
    [JsonInclude]
    [JsonRequired]
    public IReadOnlyCollection<Actor.ProfileViewBasic> Members
    {
        get;

        init
        {
            ArgumentNullException.ThrowIfNull(value);

            field = ChatCollection.Copy(value);
        }
    } = null!;

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
    /// Gets the kind of conversation, if any.
    /// </summary>
    public ConversationKind? Kind { get; init; }
}
