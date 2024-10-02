// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace idunno.AtProto.Repo
{
    /// <summary>
    /// Properties for an AT Proto record that has been retrieved from a repository.
    /// </summary>
    public record AtProtoRecord
    {
        /// <summary>
        /// Creates a new instance of at AT Proto record.
        /// </summary>
        /// <param name="uri">The <see cref="AtUri"/> of the record.</param>
        /// <param name="cid">The <see cref="AtCid"/> of the record.</param>
        [JsonConstructor]
        public AtProtoRecord(AtUri uri, AtCid cid)
        {
            Uri = uri;
            Cid = cid;
        }

        /// <summary>
        /// Gets the <see cref="AtUri"/> of the record.
        /// </summary>
        [JsonInclude]
        public AtUri Uri { get; init; }

        /// <summary>
        /// Gets the Content Identifier (<see cref="AtCid"/>) of the record.
        /// </summary>
        [JsonInclude]
        public AtCid Cid { get; init; }

        /// <summary>
        /// Gets the value of the record.
        /// </summary>
        [JsonInclude]
        public AtProtoRecordValue? Value { get; init; }

        /// <summary>
        /// A list of keys and element data that do not map to any strongly typed properties.
        /// </summary>
        [JsonExtensionData]
        public IDictionary<string, JsonElement>? ExtensionData { get; set; } = new Dictionary<string, JsonElement>();

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
