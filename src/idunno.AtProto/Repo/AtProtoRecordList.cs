// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Collections.ObjectModel;

namespace idunno.AtProto.Repo
{
    /// <summary>
    /// A list of <typeparamref name="T"/> records, with an optional cursor for pagination.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="AtProtoRecord"/>s the list contains.</typeparam>
    public class AtProtoRecordList<T> : ReadOnlyCollection <T> where T : AtProtoRecord
    {
        /// <summary>
        /// Creates a new instance of <see cref="AtProtoRecordList{T}"/>
        /// </summary>
        /// <param name="list">The list to create this instance of <see cref="AtProtoRecordList{T}"/> from.</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        public AtProtoRecordList(IList<T> list, string? cursor = null) : base(list)
        {
            Cursor = cursor;
        }

        /// <summary>
        /// An optional cursor returned by the underlying API pagination.
        /// </summary>
        public string? Cursor { get; }
    }
}
