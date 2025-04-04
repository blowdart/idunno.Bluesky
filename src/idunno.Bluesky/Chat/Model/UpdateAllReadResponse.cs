// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Chat.Model
{
    internal record UpdateAllReadResponse
    {
        public UpdateAllReadResponse(ulong updatedCount) => UpdatedCount = updatedCount;

        [JsonInclude]
        [JsonRequired]
        public ulong UpdatedCount { get; init; }
    }
}
