// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Graph.Model
{
    internal sealed record GetListBlocksResponse
    {
        [JsonConstructor]
        public GetListBlocksResponse(IList<ListView> lists, string? cursor)
        {
            Lists = lists;
            Cursor = cursor;
        }

        [JsonInclude]
        [JsonRequired]
        public IList<ListView> Lists { get; init; }

        [JsonInclude]
        public string? Cursor { get; init; }
    }
}
