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
    internal GetJoinLinkPreviewsResponse(ICollection<JoinLinkPreviewViewBase> joinLinkPreviews)
    {
        ArgumentNullException.ThrowIfNull(joinLinkPreviews);
        JoinLinkPreviews = joinLinkPreviews;
    }

    /// <summary>
    /// Gets the requested join link previews.
    /// </summary>
    [JsonRequired]
    public ICollection<JoinLinkPreviewViewBase> JoinLinkPreviews { get; internal init; }
}
