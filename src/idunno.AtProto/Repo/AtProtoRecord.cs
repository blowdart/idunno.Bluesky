// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Repo
{
    /// <summary>
    /// Properties for an AT Proto record that has been retrieved from a repository.
    /// </summary>
    /// <remarks>
    ///This differences from <see cref="AtProtoRecordValue"/> which 
    /// </remarks>
    public record AtProtoRecord
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

        /// <summary>
        /// Gets the AT URI of the record.
        /// </summary>
        /// <value>
        /// The AT URI of the record.
        /// </value>
        [JsonInclude]
        public AtUri Uri { get; internal set; }

        /// <summary>
        /// Gets the Content ID (CID) of the record.
        /// </summary>
        /// <value>
        /// The Content ID (CID) of the record.
        /// </value>
        [JsonInclude]
        public AtCid Cid { get; internal set; }

        [JsonInclude]
        /// <summary>
        /// Gets the value of the record.
        /// </summary>
        /// <value>
        /// The value of the record.
        /// </value>
        public AtProtoRecordValue? Value { get; internal set; }

        /// <summary>
        /// Gets a <see cref="StrongReference"/> for the record.
        /// </summary>
        [JsonIgnore]
        public StrongReference StrongReference
        {
            get
            {
                return new StrongReference(Uri, Cid);
            }
        }
    }
}
