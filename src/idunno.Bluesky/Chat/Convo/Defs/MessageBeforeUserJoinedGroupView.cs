// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Chat;
#pragma warning restore IDE0130 // Namespace does not match folder structure

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