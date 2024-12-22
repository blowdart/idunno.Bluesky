// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.AtProto.Repo;

namespace idunno.Bluesky.Record
{
    /// <summary>
    /// Represents the profile record of a user.
    /// </summary>
    public sealed record ProfileRecord : AtProtoRecord<ProfileRecordValue>
    {
        /// <summary>
        /// Creates a new instance of <see cref="ProfileRecord"/>.
        /// </summary>
        /// <param name="uri">The <see cref="AtUri"/> of the profile record.</param>
        /// <param name="cid">The <see cref="Cid">content identifier</see> of the profile record.</param>
        /// <param name="value">The value of the <see cref="ProfileRecordValue"/> record.</param>
        [JsonConstructor]
        public ProfileRecord(AtUri uri, Cid cid, ProfileRecordValue value) : base(uri, cid, value)
        {
            ArgumentNullException.ThrowIfNull(value);
        }
    }
}
