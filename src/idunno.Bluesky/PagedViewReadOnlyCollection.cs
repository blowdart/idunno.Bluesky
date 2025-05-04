// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Collections.ObjectModel;

namespace idunno.Bluesky
{
    /// <summary>
    /// A list of <typeparamref name="T"/> views, with an optional cursor for pagination.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="View"/>s the list contains.</typeparam>
    /// <param name="list">The list to create this instance of <see cref="PagedViewReadOnlyCollection{T}"/> from.</param>
    /// <param name="cursor">An optional cursor for pagination.</param>
    public class PagedViewReadOnlyCollection<T>(IList<T> list, string? cursor = null) : ReadOnlyCollection <T>(list) where T : View
    {
        /// <summary>
        /// Creates a new instance of <see cref="PagedViewReadOnlyCollection{T}"/> with an empty list and no cursor.
        /// </summary>
        public PagedViewReadOnlyCollection() : this([], null)
        {
        }

        /// <summary>
        /// Creates a new instance of <see cref="PagedViewReadOnlyCollection{T}"/>.
        /// </summary>
        /// <param name="collection">A collection of <typeparamref name="T"/> to create this instance of <see cref="PagedViewReadOnlyCollection{T}"/> from.</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        public PagedViewReadOnlyCollection(ICollection<T> collection, string? cursor = null) :this([.. collection], cursor)
        {
        }

        /// <summary>
        /// An optional cursor returned by the underlying API pagination.
        /// </summary>
        public string? Cursor { get; } = cursor;
    }
}
