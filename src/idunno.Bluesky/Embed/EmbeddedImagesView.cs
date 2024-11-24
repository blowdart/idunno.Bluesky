// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Embed
{
    public sealed record EmbeddedImagesView : EmbeddedView
    {
        [JsonConstructor]
        internal EmbeddedImagesView(IReadOnlyList<EmbeddedImageView> images)
        {
            Images = images;
        }

        [JsonInclude]
        [JsonRequired]
        public IReadOnlyList<EmbeddedImageView> Images { get; init; }
    }
}
