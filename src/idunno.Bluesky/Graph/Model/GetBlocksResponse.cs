// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using idunno.Bluesky.Actor;

namespace idunno.Bluesky.Graph.Model
{
    [SuppressMessage("Performance", "CA1812", Justification = "Used in GetBlocks.")]
    internal sealed record GetBlocksResponse
    {
        [JsonConstructor]
        public GetBlocksResponse(ICollection<ProfileView> blocks, string? cursor)
        {
            Blocks = blocks;
            Cursor = cursor;
        }

        [JsonInclude]
        [JsonRequired]
        public ICollection<ProfileView> Blocks { get; init; }

        [JsonInclude]
        public string? Cursor { get; init; }
    }
}
