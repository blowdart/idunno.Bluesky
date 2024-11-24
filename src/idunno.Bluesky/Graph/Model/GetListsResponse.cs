﻿// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Graph.Model
{
    internal sealed record GetListsResponse
    {
        [JsonConstructor]
        public GetListsResponse(IList<ListView> lists, string? cursor)
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