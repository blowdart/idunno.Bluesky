// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

#pragma warning disable IDE0130 // Preserve the established namespace independently of folder layout.
namespace idunno.Bluesky.Actor;
#pragma warning restore IDE0130

/// <summary>
/// Encapsulates the chat configuration for an actor.
/// </summary>
public record ProfileAssociatedChat
{
    /// <summary>
    /// Creates an instance of <see cref="ProfileAssociatedChat"/>
    /// </summary>
    /// <param name="allowIncoming">Value indicating type of chats that will be allowed.</param>
    public ProfileAssociatedChat(AllowIncomingChat allowIncoming) => AllowIncoming = allowIncoming;

    /// <summary>
    /// Gets a flag indicating the rules for incoming chats.
    /// </summary>
    public AllowIncomingChat AllowIncoming { get; init; }

    /// <summary>
    /// Gets the optional group invitation policy.
    /// </summary>
    /// <remarks>
    /// <para>Known values are <c>all</c>, <c>none</c> and <c>following</c>. Unrecognized values are preserved unchanged.</para>
    /// </remarks>
    public string? AllowGroupInvites { get; init; }
}

/// <summary>
/// Configuration values for an actor's incoming chat configuration.
/// </summary>
[JsonConverter(typeof(AllowIncomingChatConverter))]
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
    Following = 2,

    /// <summary>
    /// The chat configuration is one this library does not recognize.
    /// </summary>
    /// <remarks>
    /// <para>This value only ever comes from the service, and cannot be serialized.</para>
    /// </remarks>
    Unknown = 3
}
