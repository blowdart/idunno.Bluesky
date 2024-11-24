// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;

namespace idunno.Bluesky.Graph.Model
{
    internal record MuteActorListRequest
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
