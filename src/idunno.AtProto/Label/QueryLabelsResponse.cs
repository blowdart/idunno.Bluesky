﻿// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.LabelModels
{
    internal record QueryLabelsResponse
    {
        [JsonConstructor]
        public QueryLabelsResponse(IEnumerable<Label> labels, string? cursor)
        {
            Labels = labels;
            Cursor = cursor;
        }

        [JsonInclude]
        [JsonRequired]
        public IEnumerable<Label> Labels { get; init; } = null!;

        [JsonInclude]
        public string? Cursor { get; init; }
    }
}