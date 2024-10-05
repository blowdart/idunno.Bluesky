// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

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
        /// <param name="list">The list to create this instance from.</param>
        /// <param name="cursor">An optional cursor for validation.</param>
        public AtProtoRecordList(IList<T> list, string? cursor) : base(list)
        {
            Cursor = cursor;
        }

        /// <summary>
        /// An optional cursor for API pagination of the list.
        /// </summary>
        [JsonInclude]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Cursor { get; }
    }
}
