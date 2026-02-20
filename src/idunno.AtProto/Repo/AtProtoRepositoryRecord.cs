// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace idunno.AtProto.Repo
{
    /// <summary>
    /// Represents an <see cref="AtProtoRecord"/> retrieved from a repository.
    /// </summary>
    public record AtProtoRepositoryRecord : AtProtoRepositoryObject
    {
        /// <summary>
        /// Creates a new instance of <see cref="AtProtoRepositoryRecord"/>.
        /// </summary>
        /// <param name="uri">The <see cref="AtUri"/> of the record in an atproto repository.</param>
        /// <param name="cid">The <see cref="Cid"/> of the record in an atproto repository.</param>
        /// <param name="value">The value of the record.</param>
        public AtProtoRepositoryRecord(AtUri uri, Cid cid, JsonDocument? value) : base(uri, cid)
        {
            Value = value;
        }

        /// <summary>
        /// Gets or sets the value of the record.
        /// </summary>
        public JsonDocument? Value { get; set; }

        /// <summary>
        /// A list of keys and element data that do not map to any strongly typed properties.
        /// </summary>
        [JsonExtensionData]
        [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Needs to be settable for json deserialization")]
        public IDictionary<string, JsonElement>? ExtensionData { get; set; } = new Dictionary<string, JsonElement>();
    }

    /// <summary>
    /// Represents an <see cref="AtProtoRecord"/> retrieved from a repository.
    /// </summary>
    /// <typeparam name="TRecord">The type of the record.</typeparam>
    public record AtProtoRepositoryRecord<TRecord> : AtProtoRepositoryObject where TRecord: AtProtoRecord
    {
        /// <summary>
        /// Creates a new instance of at AT Proto record.
        /// </summary>
        /// <param name="uri">The <see cref="AtUri"/> of the record in an atproto repository.</param>
        /// <param name="cid">The <see cref="Cid"/> of the record in an atproto repository.</param>
        /// <param name="value">The value of the record.</param>
        public AtProtoRepositoryRecord(AtUri uri, Cid cid, TRecord value) : base(uri, cid)
        {
            Value = value;
        }

        /// <summary>
        /// Gets or sets the value of the record.
        /// </summary>
        [JsonRequired]
        public TRecord Value { get; set; }

        /// <summary>
        /// A list of keys and element data that do not map to any strongly typed properties.
        /// </summary>
        [JsonExtensionData]
        [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Needs to be settable for json deserialization")]
        public IDictionary<string, JsonElement>? ExtensionData { get; set; } = new Dictionary<string, JsonElement>();
    }
}
