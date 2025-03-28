// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Labels.Models
{
    internal sealed record QueryLabelsResponse
    {
        [JsonConstructor]
        public QueryLabelsResponse(List<Label> labels, string? cursor)
        {
            Labels = labels;
            Cursor = cursor;
        }

        [JsonInclude]
        [JsonRequired]
        public List<Label> Labels { get; init; } = null!;

        [JsonInclude]
        public string? Cursor { get; init; }
    }
}
