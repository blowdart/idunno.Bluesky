// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Chat.Model
{
    internal sealed record GetLogResponse
    {
        [JsonConstructor]
        public GetLogResponse(string? cursor, ICollection<LogBase> logs)
        {
            Cursor = cursor;

            Logs = new List<LogBase>(logs);
        }

        [JsonInclude]
        public string? Cursor { get; init; }

        [JsonInclude]
        [JsonRequired]
        public ICollection<LogBase> Logs { get; init; }
    }
}
