// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using System.Text.Json.Serialization;
using idunno.AtProto.Repo;

namespace idunno.Bluesky.Record
{
    /// <summary>
    /// Represents the declaration record for a labeler.
    /// </summary>
    public sealed record LabelerDeclarationRecord : AtProtoRecord<LabelerDeclarationRecordValue>
    {
        /// <summary>
        /// Creates a new instance of <see cref="LabelerDeclarationRecord"/>
        /// </summary>
        /// <param name="uri">The <see cref="AtUri" /> of the record.</param>
        /// <param name="cid">The <see cref="AtProto.Cid"/> of the record.</param>
        /// <param name="value">The value of the record.</param>
        [JsonConstructor]
        public LabelerDeclarationRecord(AtUri uri, Cid cid, LabelerDeclarationRecordValue value) : base(uri, cid, value)
        {
            Value = value;
        }
    }
}
