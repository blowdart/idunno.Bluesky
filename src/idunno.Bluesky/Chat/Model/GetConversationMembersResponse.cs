// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using idunno.Bluesky.Actor;

namespace idunno.Bluesky.Chat.Model;

[SuppressMessage("Performance", "CA1812", Justification = "Used in GetMessages.")]
[method: JsonConstructor]
internal record GetConversationMembersResponse(string? Cursor, ICollection<ProfileViewBasic> Members)
{
}
