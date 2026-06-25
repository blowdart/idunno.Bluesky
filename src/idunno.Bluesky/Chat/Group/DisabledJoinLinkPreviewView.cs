// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace idunno.Bluesky.Chat.Group;

/// <summary>
/// Preview for a disabled join link. Carries only the code so clients can correlate with the input and render a disabled state.
/// </summary>
public sealed record DisabledJoinLinkPreviewView : JoinLinkPreviewViewBase
{
    internal DisabledJoinLinkPreviewView(string code)
    {
        Code = code;
    }

    /// <summary>
    /// Gets the join link code for the group conversation.
    /// </summary>
    [JsonRequired]
    public string Code { get; init; }
}
