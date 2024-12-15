// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace idunno.AtProto.Repo
{
    /// <summary>
    /// Properties for an AT Proto record that has been retrieved from a repository.
    /// </summary>
    public record AtProtoRecord : AtProtoReferencedObject
    {
        /// <summary>
        /// Creates a new instance of at AT Proto record.
        /// </summary>
        /// <param name="uri">The <see cref="AtUri"/> of the record.</param>
        /// <param name="cid">The <see cref="Cid"/> of the record.</param>
        /// <param name="value">The value of the record.</param>
        [JsonConstructor]
        public AtProtoRecord(AtUri uri, Cid cid, AtProtoRecordValue? value) : base(uri, cid)
        {
            Value = value;
        }

        /// <summary>
        /// Gets the value of the record.
        /// </summary>
        [JsonInclude]
        public AtProtoRecordValue? Value { get; init; }

        /// <summary>
        /// A list of keys and element data that do not map to any strongly typed properties.
        /// </summary>
        [JsonExtensionData]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Needs to be settable for json deserialization")]
        public IDictionary<string, JsonElement>? ExtensionData { get; set; } = new Dictionary<string, JsonElement>();
    }

    /// <summary>
    /// Properties for an AT Proto record that has been retrieved from a repository.
    /// </summary>
    public record AtProtoRecord<T> : AtProtoReferencedObject where T: AtProtoRecordValue
    {
        /// <summary>
        /// Creates a new instance of at AT Proto record.
        /// </summary>
        /// <param name="uri">The <see cref="AtUri"/> of the record.</param>
        /// <param name="cid">The <see cref="Cid"/> of the record.</param>
        /// <param name="value">The value of the record.</param>
        [JsonConstructor]
        public AtProtoRecord(AtUri uri, Cid cid, T value) : base(uri, cid)
        {
            Value = value;
        }

        /// <summary>
        /// Gets the value of the record.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public T Value { get; init; }

        /// <summary>
        /// A list of keys and element data that do not map to any strongly typed properties.
        /// </summary>
        [JsonExtensionData]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Needs to be settable for json deserialization")]
        public IDictionary<string, JsonElement>? ExtensionData { get; set; } = new Dictionary<string, JsonElement>();
    }
}
