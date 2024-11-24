// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Embed
{
    // https://github.com/bluesky-social/atproto/blob/main/lexicons/app/bsky/embed/defs.json

    /// <summary>
    /// Represents an aspect ratio. It may be approximate, and may not correspond to absolute dimensions in any given unit
    /// </summary>
    public sealed record AspectRatio
    {
        [JsonConstructor]
        public AspectRatio(int width, int height)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
            ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);

            Width = width;
            Height = height;
        }

        [JsonInclude]
        [JsonRequired]
        public int Width { get; init; }

        [JsonInclude]
        [JsonRequired]
        public int Height { get; init; }
    }
}
