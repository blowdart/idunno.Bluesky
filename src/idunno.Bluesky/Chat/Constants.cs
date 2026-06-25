// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Chat;

/// <summary>
/// Type discriminators for message objects
/// </summary>
public static class MessageTypeDiscriminators
{
    /// <summary>
    /// A view over a message.
    /// </summary>
    public const string MessageView = "chat.bsky.convo.defs#messageView";

    /// <summary>
    /// A view over a deleted message.
    /// </summary>
    public const string DeletedMessageView = "chat.bsky.convo.defs#deletedMessageView";

    /// <summary>
    /// A view over a system message.
    /// </summary>
    public const string SystemMessageView = "chat.bsky.convo.defs#systemMessageView";
}

/// <summary>
/// Known values for conversation member roles.
/// </summary>
public static class MemberRole
{
    /// <summary>
    /// Indicates a conversation member that is the owner of the conversation.
    /// </summary>
    public const string Owner = "owner";

    /// <summary>
    /// Indicates a conversation member that is a standard member of the conversation.
    /// </summary>
    public const string Standard = "standard";
}

/// <summary>
/// Known values for the lock status of a conversation.
/// </summary>
public static class ConversationLockStatus
{
    /// <summary>
    /// Indicates a conversation that is unlocked.
    /// </summary>
    public const string Unlocked = "unlocked";

    /// <summary>
    /// Indicates a conversation that is locked.
    /// </summary>
    public const string Locked = "locked";

    /// <summary>
    /// Indicates a conversation that is permanently locked.
    /// </summary>
    public const string LockedPermanently = "locked-permanently";
}

/// <summary>
/// Known values for the status of a conversation.
/// </summary>
public static class ConversationStatus
{
    /// <summary>
    /// Indicates a conversation that has been accepted.
    /// </summary>
    public const string Accepted = "accepted";

    /// <summary>
    /// Indicates a conversation that is a request.
    /// </summary>
    public const string Requested = "request";
}