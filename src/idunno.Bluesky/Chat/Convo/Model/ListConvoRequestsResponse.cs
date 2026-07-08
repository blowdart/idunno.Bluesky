// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Chat.Convo.Model;

internal record ListConvoRequestsResponse(string? Cursor, [field: JsonRequired] ICollection<ConversationViewBase> Requests)
{
}