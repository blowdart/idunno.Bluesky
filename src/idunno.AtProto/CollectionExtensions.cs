// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto;

/// <summary>
/// Provides extension methods for collections.
/// </summary>
public static class CollectionHelpers
{
    /// <summary>
    /// Adds the elements of the specified collection to the end of the <see cref="ICollection{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="destination">The collection to add elements to.</param>
    /// <param name="collection">The collection of elements to add.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="destination"/> is <see langword="null"/>.</exception>
    public static void AddRange<T>(this ICollection<T> destination, IEnumerable<T> collection)
    {
        ArgumentNullException.ThrowIfNull(destination);

        if (collection is null)
        {
            return;
        }

        foreach (T item in collection)
        {
            destination.Add(item);
        }
    }
}