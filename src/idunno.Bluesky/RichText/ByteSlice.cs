// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.RichText
{
    public sealed record ByteSlice
    {
        /// <summary>
        /// Constructs a new instance of <see cref="ByteSlice"/>.
        /// </summary>
        /// <param name="byteStart">The byte index at which the facet starts.</param>
        /// <param name="byteEnd">The byte index at which the facet ends.</param>
        /// <remarks>
        /// <para><paramref name="ByteStart"/> is zero-indexed and inclusive.</para>
        /// <para><paramref name="ByteEnd"/> is zero-indexed and exclusive.</para>
        /// </remarks>
        [JsonConstructor]
        public ByteSlice(long byteStart, long byteEnd)
        {
            ByteStart = byteStart;
            ByteEnd = byteEnd;
        }

        /// <summary>
        /// Gets the byte index at which the facet starts.
        /// </summary>
        /// <remarks>
        /// <para>This is is zero-indexed and inclusive.</para>
        /// </remarks>
        [JsonInclude]
        [JsonRequired]
        public long ByteStart { get; init; }

        /// <summary>
        /// Gets the byte index at which the facet ends.
        /// </summary>
        /// <remarks>
        /// <para>This is is zero-indexed and exclusive.</para>
        /// </remarks>
        [JsonInclude]
        [JsonRequired]
        public long ByteEnd { get; init; }
    }
}
