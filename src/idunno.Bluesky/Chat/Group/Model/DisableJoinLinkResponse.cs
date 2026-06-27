// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Chat.Group.Model;

/// <summary>
/// The response from the API when disabling a join link for a group chat.
/// </summary>
public record DisableJoinLinkResponse
{
    [JsonConstructor]
    internal DisableJoinLinkResponse(JoinLinkView joinLink)
    {
        JoinLink = joinLink;
    }

    /// <summary>
    /// Gets the <see cref="JoinLinkView"/> of the updated join link.
    /// </summary>
    [JsonRequired]
    public JoinLinkView JoinLink { get; internal init; }
}