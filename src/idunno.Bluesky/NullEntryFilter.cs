// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky;

/// <summary>
/// Removes <see langword="null"/> entries from collections deserialized from remote JSON.
/// </summary>
/// <remarks>
/// <para>Nullable annotations on a collection property do not validate its elements, so a server can supply a <see langword="null"/>
/// entry inside a collection declared as non-nullable. These helpers are used by <c>init</c> accessors on hot deserialization
/// paths, so they return the original collection, without copying it, when it contains no <see langword="null"/> entries.</para>
/// </remarks>
internal static class NullEntryFilter
{
    /// <summary>
    /// Returns <paramref name="value"/> without any <see langword="null"/> entries.
    /// </summary>
    /// <typeparam name="T">The type of the collection entries.</typeparam>
    /// <param name="value">The collection to filter.</param>
    /// <returns>
    /// An empty collection if <paramref name="value"/> is <see langword="null"/>, <paramref name="value"/> itself if it contains no
    /// <see langword="null"/> entries, or otherwise a copy of <paramref name="value"/> with the <see langword="null"/> entries removed.
    /// </returns>
    public static IReadOnlyCollection<T> WithoutNullEntries<T>(IReadOnlyCollection<T>? value) where T : class
    {
        if (value is null)
        {
            return [];
        }

        if (value.Count == 0)
        {
            return value;
        }

        return value.Any(entry => entry is null) ? [.. value.Where(item => item is not null)] : value;
    }
}
