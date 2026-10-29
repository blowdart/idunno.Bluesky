// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Chat.Group.Model;

/// <summary>
/// The response from the getJoinLinkPreviews operation.
/// </summary>
public sealed record GetJoinLinkPreviewsResponse
{
    [JsonConstructor]
    internal GetJoinLinkPreviewsResponse(IReadOnlyCollection<JoinLinkPreviewViewBase> joinLinkPreviews)
    {
        ArgumentNullException.ThrowIfNull(joinLinkPreviews);
        JoinLinkPreviews = joinLinkPreviews;
    }

    /// <summary>
    /// Gets the requested join link previews.
    /// </summary>
    /// <exception cref="ArgumentNullException">The collection is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The collection contains a <see langword="null"/> entry.</exception>
    [JsonRequired]
    public IReadOnlyCollection<JoinLinkPreviewViewBase> JoinLinkPreviews
    {
        get;
        init => field = ChatCollection.Copy(value);
    } = null!;
}
