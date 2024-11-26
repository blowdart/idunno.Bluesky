// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using idunno.AtProto;

namespace idunno.Bluesky.Graph.Model
{
    [SuppressMessage("Performance", "CA1812", Justification = "Used in MuteActor.")]
    internal sealed record MuteActorListRequest
    {
        public MuteActorListRequest(AtUri listUri)
        {
            ListUri = listUri;
        }

        [JsonInclude]
        [JsonRequired]
        [JsonPropertyName("list")]
        public AtUri ListUri { get; init; }
    }
}
