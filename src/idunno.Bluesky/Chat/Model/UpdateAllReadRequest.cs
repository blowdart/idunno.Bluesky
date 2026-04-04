// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Chat.Model;

internal record UpdateAllReadRequest
{
    public UpdateAllReadRequest(ConversationStatus status)
    {
        Status = status;
    }

    [JsonInclude]
    [JsonRequired]
    public ConversationStatus Status { get; init; }
}
