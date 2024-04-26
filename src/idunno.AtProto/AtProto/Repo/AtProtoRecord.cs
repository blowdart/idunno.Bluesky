// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Repo
{
    /// <summary>
    /// An record defining common properties for an AT Proto record
    /// </summary>
    public class AtProtoRecord
    {
        /// <summary>
        /// Creates a new instance of at AT Proto record.
        /// </summary>
        /// <param name="type">The type of the record to be created.</param>
        [JsonConstructor]
        public AtProtoRecord(AtUri uri, AtCid cid)
        {
            Uri = uri;
            Cid = cid;
        }

        [JsonInclude]
        public AtUri Uri { get; protected set; }

        [JsonInclude]
        public AtCid Cid { get; protected set; }

        [JsonInclude]
        /// <summary>
        /// Gets the <see cref="AtProtoRecordValue"/> for this record.
        /// </summary>
        /// <value>
        /// The <see cref="AtProtoRecordValue"/> for this record.
        /// </value>
        public AtProtoRecordValue? Value { get; protected set; }
    }
}
