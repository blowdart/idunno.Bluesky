// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using idunno.AtProto.Labels;

namespace idunno.AtProto.Models
{
    [SuppressMessage("Performance", "CA1812", Justification = "Used in QueryLabels.")]
    internal sealed record QueryLabelsResponse
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
