// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.AtProto.Repo;

namespace idunno.Bluesky.Record
{
    /// <summary>
    /// Encapsulates the record for a verification entry.
    /// </summary>
    public sealed record VerificationRecord : AtProtoRecord<VerificationRecordValue>
    {
        /// <summary>
        /// Creates a new instance of <see cref="VerificationRecord"/>.
        /// </summary>
        /// <param name="uri">The <see cref="AtUri"/> of the verification record.</param>
        /// <param name="cid">The <see cref="Cid">content identifier</see> of the verification record.</param>
        /// <param name="value">The value of the <see cref="VerificationRecordValue"/> record.</param>
        [JsonConstructor]
        public VerificationRecord(AtUri uri, Cid cid, VerificationRecordValue value) : base(uri, cid, value)
        {
            ArgumentNullException.ThrowIfNull(value);
        }
    }
}
