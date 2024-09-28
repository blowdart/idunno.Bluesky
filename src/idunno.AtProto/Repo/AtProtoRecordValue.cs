// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace idunno.AtProto.Repo
{
    /// <summary>
    /// A base record for the value property on an <see cref="AtProtoRecord"/>
    /// </summary>
    public record AtProtoRecordValue
    {
        public AtProtoRecordValue() : this(DateTimeOffset.Now)
        {
        }

        [JsonConstructor]
        public AtProtoRecordValue(DateTimeOffset createdAt)
        {
            CreatedAt = createdAt;
        }

        /// <summary>
        /// Gets the <see cref="DateTimeOffset"/> the record was created at.
        /// </summary>
        /// <value>
        /// The <see cref="DateTimeOffset"/> the record was created at.
        /// </value>
        [JsonInclude]
        public DateTimeOffset CreatedAt { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Values { get; } = new Dictionary<string, JsonElement>();
    }
}
