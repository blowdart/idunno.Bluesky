// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Chat.Group;

/// <summary>
/// Preview for a join link code that does not map to an existing link. Carries only the code so clients can correlate with the input and render an invalid state
/// </summary>
public sealed record InvalidJoinLinkPreviewView : JoinLinkPreviewViewBase
{
    internal InvalidJoinLinkPreviewView(string code)
    {
        Code = code;
    }

    /// <summary>
    /// Gets the join link code for the group conversation.
    /// </summary>
    [JsonRequired]
    public string Code { get; init; }
}
