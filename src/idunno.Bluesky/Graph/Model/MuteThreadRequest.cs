// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;

namespace idunno.Bluesky.Graph.Model
{
    internal record MuteThreadRequest
    {
        public MuteThreadRequest(AtUri root)
        {
            Root = root;
        }

        [JsonInclude]
        [JsonRequired]
        public AtUri Root { get; init; }
    }
}
