// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.AtProto.Repo;

namespace idunno.Bluesky.Record
{
    /// <summary>
    /// Encapsulates a Post record.
    /// </summary>
    public sealed record PostRecord : AtProtoRecord<Post>
    {
        /// <summary>
        /// Creates a new instance of <see cref="PostRecord"/>
        /// </summary>
        /// <param name="uri">The <see cref="AtUri" /> of the record.</param>
        /// <param name="cid">The <see cref="AtProto.Cid"/> of the record.</param>
        /// <param name="value">The value of the record.</param>
        [JsonConstructor]
        public PostRecord(AtUri uri, Cid cid, Post value) : base(uri, cid, value)
        {
            Value = value;
        }
    }
}
