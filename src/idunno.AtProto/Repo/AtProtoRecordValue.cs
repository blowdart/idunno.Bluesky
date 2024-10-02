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
        /// <summary>
        /// Creates a new instance of <see cref="AtProtoRecord"/> populating the CreatedAt property
        /// with the current date and time.
        /// </summary>
        public AtProtoRecordValue() : this(DateTimeOffset.Now)
        {
        }

        /// <summary>
        /// Creates a new instance of <see cref="AtProtoRecord"/>.
        /// </summary>
        /// <param name="createdAt">The <see cref="DateTimeOffset"/> the record was created at.</param>
        [JsonConstructor]
        public AtProtoRecordValue(DateTimeOffset createdAt)
        {
            CreatedAt = createdAt;
        }

        /// <summary>
        /// Gets or sets the <see cref="DateTimeOffset"/> this <see cref="AtProtoRecord"/> was created at.
        /// </summary>
        [JsonInclude]
        public DateTimeOffset CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets the json type of the record.
        /// </summary>
        [JsonIgnore]
        [JsonPropertyName("$type")]
        public string? Type { get; set; }

        /// <summary>
        /// A list of keys and element data that do not map to any strongly typed properties.
        /// </summary>
        [JsonExtensionData]
        public IDictionary<string, JsonElement>? ExtensionData { get; set; } = new Dictionary<string, JsonElement>();
    }
}
