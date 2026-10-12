// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.Bluesky.Chat.Group;
using idunno.Bluesky.Embed;

namespace idunno.Bluesky.Chat.Embed;

/// <summary>
/// Encapsulates a join link preview embedded in a chat message.
/// </summary>
public sealed record JoinLinkView : EmbeddedView
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JoinLinkView"/> class.
    /// </summary>
    /// <param name="joinLinkPreview">The preview of the join link.</param>
    /// <exception cref="ArgumentNullException"><paramref name="joinLinkPreview"/> is <see langword="null"/>.</exception>
    [JsonConstructor]
    public JoinLinkView(JoinLinkPreviewViewBase joinLinkPreview)
    {
        ArgumentNullException.ThrowIfNull(joinLinkPreview);
        JoinLinkPreview = joinLinkPreview;
    }

    /// <summary>
    /// Gets the preview of the join link.
    /// </summary>
    [JsonRequired]
    public JoinLinkPreviewViewBase JoinLinkPreview { get; init; }
}
