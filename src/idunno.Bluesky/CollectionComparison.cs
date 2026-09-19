// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky;

/// <summary>
/// Helpers for comparing and hashing collection properties by value rather than by reference.
/// </summary>
internal static class CollectionComparison
{
    /// <summary>
    /// Determines whether two collections contain the same elements, in the same order.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the collections.</typeparam>
    /// <param name="left">The first collection to compare.</param>
    /// <param name="right">The second collection to compare.</param>
    /// <returns>
    /// <see langword="true" /> if both collections are <see langword="null" />, or if both are non-null and
    /// contain the same elements in the same order, otherwise <see langword="false" />.
    /// </returns>
    public static bool SequenceEquals<T>(IEnumerable<T>? left, IEnumerable<T>? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        return left.SequenceEqual(right);
    }

    /// <summary>
    /// Adds the elements of <paramref name="collection"/> to <paramref name="hashCode"/>.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the collection.</typeparam>
    /// <param name="hashCode">The <see cref="HashCode"/> to add the elements to.</param>
    /// <param name="collection">The collection whose elements should be added, which may be <see langword="null" />.</param>
    public static void AddSequence<T>(ref HashCode hashCode, IEnumerable<T>? collection)
    {
        if (collection is null)
        {
            hashCode.Add(0);
            return;
        }

        foreach (T item in collection)
        {
            hashCode.Add(item);
        }
    }
}
