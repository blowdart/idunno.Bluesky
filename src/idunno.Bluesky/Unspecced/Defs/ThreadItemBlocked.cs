// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.Bluesky.Feed;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Unspecced;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Indicates that the thread item is blocked.
/// </summary>
public sealed record ThreadItemBlocked : ThreadItemValue
{
    [JsonConstructor]
    internal ThreadItemBlocked(BlockedAuthor author)
    {
        Author = author;
    }

    /// <summary>
    /// Gets the <see cref="BlockedAuthor"/> whose post is blocked.
    /// </summary>
    [JsonRequired]
    [JsonInclude]
    public BlockedAuthor Author { get; init; }
}
