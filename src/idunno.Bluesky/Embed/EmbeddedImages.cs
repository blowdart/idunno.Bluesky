// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Embed
{
    public record EmbeddedImages : EmbeddedMediaBase
    {
        public EmbeddedImages(ICollection<EmbeddedImage> images)
        {
            ArgumentNullException.ThrowIfNull(images);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(images.Count, Maximum.ImagesInPost);

            Images = images;
        }

        [JsonInclude]
        public ICollection<EmbeddedImage> Images { get; init; }
    }
}
