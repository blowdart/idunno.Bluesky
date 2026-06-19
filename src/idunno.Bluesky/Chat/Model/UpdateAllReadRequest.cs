// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Chat.Model;

internal record UpdateAllReadRequest
{
    public UpdateAllReadRequest(string status)
    {
        Status = status;
    }

    [JsonInclude]
    [JsonRequired]
    public string Status { get; init; }
}