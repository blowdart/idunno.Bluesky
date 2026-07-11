// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Feed;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Specifies the order of ranking of search results in the v2 search API.
/// </summary>
public enum SortOrder
{
    /// <summary>
    /// Ranks order by top posts.
    /// </summary>
    Top,

    /// <summary>
    /// Ranks order by recent posts.
    /// </summary>
    Recent
}