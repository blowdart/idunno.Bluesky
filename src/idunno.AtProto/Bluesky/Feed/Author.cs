// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Bluesky.Feed
{
    public record Author
    {
        [JsonConstructor]
        public Author(Did did, Handle handle, Viewer viewer)
        {
            Did = did;
            Handle = handle;
            Viewer = viewer;
        }

        [JsonInclude]
        [JsonRequired]
        public Did Did { get; internal set; }

        [JsonInclude]
        public string? DisplayName { get; internal set; }

        [JsonInclude]
        [JsonRequired]
        public Handle Handle { get; internal set; }

        [JsonInclude]
        [JsonRequired]
        public Viewer Viewer { get; internal set; }


        // ToDo : mutedByList, blockingByList
    }
}
