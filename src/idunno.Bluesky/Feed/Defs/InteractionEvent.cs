// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Feed;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// The kinds of interaction with a feed item a client can report to a feed generator.
/// </summary>
[JsonConverter(typeof(InteractionEventConverter))]
public enum InteractionEvent
{
    /// <summary>
    /// The user asked to see less content like the item.
    /// </summary>
    RequestLess,

    /// <summary>
    /// The user asked to see more content like the item.
    /// </summary>
    RequestMore,

    /// <summary>
    /// The user clicked through to the item.
    /// </summary>
    ClickthroughItem,

    /// <summary>
    /// The user clicked through to the author of the item.
    /// </summary>
    ClickthroughAuthor,

    /// <summary>
    /// The user clicked through to the reposter of the item.
    /// </summary>
    ClickthroughReposter,

    /// <summary>
    /// The user clicked through to the embed of the item.
    /// </summary>
    ClickthroughEmbed,

    /// <summary>
    /// The item was seen by the user.
    /// </summary>
    InteractionSeen,

    /// <summary>
    /// The user liked the item.
    /// </summary>
    InteractionLike,

    /// <summary>
    /// The user reposted the item.
    /// </summary>
    InteractionRepost,

    /// <summary>
    /// The user replied to the item.
    /// </summary>
    InteractionReply,

    /// <summary>
    /// The user quoted the item.
    /// </summary>
    InteractionQuote,

    /// <summary>
    /// The user shared the item.
    /// </summary>
    InteractionShare,

    /// <summary>
    /// The user followed the author of the item.
    /// </summary>
    InteractionFollow,

    /// <summary>
    /// An interaction which has no more specific event.
    /// </summary>
    InteractionUnspecced,

    /// <summary>
    /// The event is not one this library recognizes. It cannot be sent to a service.
    /// </summary>
    Unknown
}
