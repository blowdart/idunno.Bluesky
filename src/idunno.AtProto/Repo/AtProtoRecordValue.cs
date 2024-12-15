// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace idunno.AtProto.Repo
{
    /// <summary>
    /// A base record for the value property on an <see cref="AtProtoRecord{T}"/>.
    /// </summary>
    public record AtProtoRecordValue
    {
        /// <summary>
        /// Creates a new instance of <see cref="AtProtoRecordValue"/>
        /// </summary>
        [JsonConstructor]
        public AtProtoRecordValue() { }

        /// <summary>
        /// A list of keys and element data that do not map to any strongly typed properties.
        /// </summary>
        [JsonExtensionData]
        public IDictionary<string, JsonElement>? ExtensionData { get; set; } = new Dictionary<string, JsonElement>();
    }
}
