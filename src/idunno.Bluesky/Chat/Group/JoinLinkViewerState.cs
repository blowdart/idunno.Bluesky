// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Chat.Group;

/// <summary>
/// Represents the state of a join link viewer, which indicates whether the viewer has requested to join the group chat via the join link and when that request was made.
/// </summary>
public sealed record JoinLinkViewerState
{
    /// <summary>
    /// Creates a new instance of the <see cref="JoinLinkViewerState"/> record.
    /// </summary>
    /// <param name="requestedAt">The <see cref="DateTimeOffset"/> the join link was requested at, or <see langword="null"/> if unknown.</param>
    [JsonConstructor]
    public JoinLinkViewerState(DateTimeOffset? requestedAt)
    {
        RequestedAt = requestedAt;
    }

    /// <summary>
    /// Gets the <see cref="DateTimeOffset"/> the join link was requested at, or <see langword="null"/> if unknown.
    /// </summary>
    public DateTimeOffset? RequestedAt { get; init; }
}
