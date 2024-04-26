// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Repo
{
    public abstract class NewAtProtoRecord
    {
        /// <summary>
        /// Creates a new instance of <see cref="NewAtProtoRecord"/>
        /// </summary>
        /// <param name="type">The type of the record to be created.</param>
        internal NewAtProtoRecord(string type)
        {
            Type = type;
        }

        /// <summary>
        /// Gets the record type of this instance.
        /// </summary>
        /// <value>
        /// The record type of this instance.
        /// </value>
        /// <remarks>
        /// This is the type discriminator in JSON, not the .NET class type.
        /// </remarks>
        [JsonPropertyName("$type")]
        [JsonInclude]
        public string Type { get; protected set; }

        /// <summary>
        /// Gets the <see cref="DateTimeOffset"/> the record was created at.
        /// </summary>
        /// <value>
        /// The <see cref="DateTimeOffset"/> the record was created at.
        /// </value>
        [JsonInclude]
        public DateTimeOffset CreatedAt { get; protected set; } = DateTimeOffset.Now;

        /// <summary>
        /// Gets a collection of property name and values written during serialization or added to the dictionary during deserialization.
        /// </summary>
        /// <value>
        /// A collection of property name and values written during serialization or added to the dictionary during deserialization.
        /// </value>
        [JsonExtensionData]
        public IDictionary<string, object> Values { get; } = new Dictionary<string, object>();
    }
}
