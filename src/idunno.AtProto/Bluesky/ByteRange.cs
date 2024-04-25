// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Bluesky
{
    /// <summary>
    /// The byte range at which a facet applies.
    /// </summary>
    /// <param name="ByteStart">The byte index at which the facet applies to.</param>
    /// <param name="ByteEnd">The byte index at which the facet ends.</param>
    /// <remarks>
    /// <paramref name="ByteStart"/> is zero-indexed and inclusive.
    /// <paramref name="ByteEnd"/> is zero-indexed and exclusive.
    public record ByteRange(long ByteStart, long ByteEnd);
}
