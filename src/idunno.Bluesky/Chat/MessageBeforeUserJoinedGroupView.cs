// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Chat;

/// <summary>
/// Placeholder embedded in place of a reply's parent message when that parent was sent before the viewer joined the group conversation.
/// The viewer has no access to that history, so no message data is carried.
/// </summary>
public sealed record MessageBeforeUserJoinedGroupView : MessageViewBase
{
    /// <summary>
    /// Creates a new instance of <see cref="MessageBeforeUserJoinedGroupView"/>.
    /// </summary>
    [JsonConstructor]
    public MessageBeforeUserJoinedGroupView()
    {
    }
}
