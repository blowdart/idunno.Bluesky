// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using System.Text.Json;
using System.Text.Json.Serialization;

namespace idunno.AtProto.Repo
{
    /// <summary>
    /// A base record for the value property on an <see cref="AtProtoRepositoryRecord{TRecord}"/>.
    /// </summary>
    public record AtProtoRecord
    {
        /// <summary>
        /// Creates a new instance of <see cref="AtProtoRecord"/>
        /// </summary>
        [JsonConstructor]
        public AtProtoRecord() { }

        /// <summary>
        /// Creates a new instance of <see cref="AtProtoRecord"/> from the specified <paramref name="record"/>.
        /// </summary>
        /// <param name="record">The <see cref="AtProtoRecord"/> to create the new instance from.</param>
        public AtProtoRecord(AtProtoRecord record)
        {
            if (record is not null)
            {
                ExtensionData = new Dictionary<string, JsonElement>(record.ExtensionData);
            }
        }

        /// <summary>
        /// A list of keys and element data that do not map to any strongly typed properties.
        /// </summary>
        [NotNull]
        [ExcludeFromCodeCoverage]
        [JsonExtensionData]
        [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Needs to be settable for json deserialization")]
        public IDictionary<string, JsonElement>? ExtensionData { get; set; } = new Dictionary<string, JsonElement>();
    }
}
