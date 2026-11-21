// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

using idunno.AtProto;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Feed;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// An interaction with a feed item, reported back to the feed generator that served it.
/// </summary>
public sealed record Interaction
{
    /// <summary>
    /// The maximum length, in UTF-8 bytes, of <see cref="FeedContext"/>.
    /// </summary>
    public const int MaximumFeedContextLength = 2000;

    /// <summary>
    /// The maximum length, in UTF-8 bytes, of <see cref="RequestId"/>.
    /// </summary>
    public const int MaximumRequestIdLength = 100;

    /// <summary>
    /// Creates a new instance of <see cref="Interaction"/>.
    /// </summary>
    /// <param name="item">The <see cref="AtUri"/> of the feed item the interaction was with.</param>
    /// <param name="event">The kind of interaction.</param>
    /// <param name="viewContext">The view in which the interaction occurred.</param>
    /// <param name="payload">Additional application-defined data for the interaction.</param>
    /// <param name="feedContext">The <see cref="SkeletonFeedPost.FeedContext"/> the feed generator supplied for the item.</param>
    /// <param name="requestId">The <see cref="FeedSkeleton.RequestId"/> of the request which returned the item.</param>
    [JsonConstructor]
    public Interaction(
        AtUri? item = null,
        InteractionEvent? @event = null,
        InteractionViewContext? viewContext = null,
        JsonElement? payload = null,
        string? feedContext = null,
        string? requestId = null)
    {
        Item = item;
        Event = @event;
        ViewContext = viewContext;
        Payload = payload;
        FeedContext = feedContext;
        RequestId = requestId;
    }

    /// <summary>
    /// Gets the <see cref="AtUri"/> of the feed item the interaction was with.
    /// </summary>
    [JsonInclude]
    public AtUri? Item { get; init; }

    /// <summary>
    /// Gets the kind of interaction.
    /// </summary>
    [JsonInclude]
    public InteractionEvent? Event { get; init; }

    /// <summary>
    /// Gets the view in which the interaction occurred.
    /// </summary>
    [JsonInclude]
    public InteractionViewContext? ViewContext { get; init; }

    /// <summary>
    /// Gets additional application-defined data for the interaction.
    /// </summary>
    [JsonInclude]
    public JsonElement? Payload { get; init; }

    /// <summary>
    /// Gets the <see cref="SkeletonFeedPost.FeedContext"/> the feed generator supplied for the item.
    /// </summary>
    /// <remarks>
    /// <para>Must be no longer than <see cref="MaximumFeedContextLength"/> UTF-8 bytes when sent.</para>
    /// </remarks>
    [JsonInclude]
    public string? FeedContext { get; init; }

    /// <summary>
    /// Gets the <see cref="FeedSkeleton.RequestId"/> of the request which returned the item.
    /// </summary>
    /// <remarks>
    /// <para>Must be no longer than <see cref="MaximumRequestIdLength"/> UTF-8 bytes when sent.</para>
    /// </remarks>
    [JsonInclude]
    [JsonPropertyName("reqId")]
    public string? RequestId { get; init; }
}
