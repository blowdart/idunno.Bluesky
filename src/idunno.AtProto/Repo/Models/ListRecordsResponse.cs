// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Nodes;

namespace idunno.AtProto.Repo.Models
{
    internal sealed record ListRecordsResponse(ICollection<JsonObject> Records)
    {
        public string? Cursor { get; set; }
    }
}
