// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Chat.Actor;

/// <summary>
/// Encapsulates the chat status of an actor.
/// </summary>
/// <param name="ChatDisabled"><see langword="true" /> when the viewer's account is disabled and cannot actively participate in chat.</param>
/// <param name="CanCreateGroups">Flag indicating whether the viewer's account is allowed to create group chats. New accounts are restricted from creating groups.</param>
/// <param name="GroupMemberLimit">The maximum number of members allowed in a group conversation.</param>
public sealed record ChatStatus([field: JsonRequired] bool ChatDisabled, [field: JsonRequired] bool CanCreateGroups, [field: JsonRequired] int GroupMemberLimit)
{
}
