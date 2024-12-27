// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Collections.ObjectModel;

using idunno.AtProto.Repo;

namespace idunno.AtProto
{
    /// <summary>
    /// A list of <typeparamref name="T"/> <see cref="AtProtoObject"/>s, with an optional cursor for pagination.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="AtProtoObject"/>s the list contains.</typeparam>
    public class PagedReadOnlyCollection<T> : ReadOnlyCollection<T> where T : AtProtoObject
    {
        /// <summary>
        /// Creates a new instance of <see cref="PagedReadOnlyCollection{T}"/>
        /// </summary>
        /// <param name="list">The list to create this instance of <see cref="PagedReadOnlyCollection{T}"/> from.</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        public PagedReadOnlyCollection(IList<T> list, string? cursor = null) : base(list)
        {
            Cursor = cursor;
        }

        /// <summary>
        /// Creates a new instance of <see cref="PagedReadOnlyCollection{T}"/>
        /// </summary>
        /// <param name="collection">A collection of <typeparamref name="T"/> to create this instance of <see cref="PagedReadOnlyCollection{T}"/> from.</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        public PagedReadOnlyCollection(IEnumerable<T> collection, string? cursor = null) : this(new List<T>(collection), cursor)
        {
        }

        /// <summary>
        /// Creates a new instance of <see cref="PagedReadOnlyCollection{T}"/> with an empty list.
        /// </summary>
        /// <param name="cursor">An optional cursor for pagination.</param>
        public PagedReadOnlyCollection(string? cursor = null) : this(new List<T>(), cursor)
        {
        }

        /// <summary>
        /// An optional cursor returned by the underlying API pagination.
        /// </summary>
        public string? Cursor { get; }
    }
}
