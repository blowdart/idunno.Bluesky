// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Labels
{
    /// <summary>
    /// Metadata tag on an atproto record, published by the author within the record.
    /// </summary>
    public record SelfLabel
    {
        /// <summary>
        /// Creates a new instance of <see cref="SelfLabel"/>
        /// </summary>
        /// <param name="value">The short string name of the value or type of this label.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="value"/> is null or whitespace.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="value"/> is longer than 128 characters.</exception>
        public SelfLabel(string value)
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(value);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value.Length, 128);

            Value = value;
        }

        /// <summary>
        /// The short string name of the value or type of this label.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        [JsonPropertyName("val")]
        public string Value { get; init; }
    }
}
