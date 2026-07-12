// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Chat.Group.Model;

/// <summary>
/// The response from a call to create a join link.
/// </summary>
public sealed record CreateJoinLinkResponse
{
    [JsonConstructor]
    internal CreateJoinLinkResponse(JoinLinkView joinLink)
    {
        JoinLink = joinLink;
    }

    /// <summary>
    /// Gets a view of the join link.
    /// </summary>
    [JsonRequired]
    public JoinLinkView JoinLink { get; init; }
}