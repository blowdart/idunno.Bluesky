// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto
{
    /// <summary>
    /// Provides an object representation of an Content Identifier (CID).
    /// </summary>
    /// <remarks>
    /// https://github.com/multiformats/cid
    /// </remarks>
    public sealed class AtCid
    {
        /// <summary>
        /// Creates a new instance of a <see cref="AtCid"/> class using the specified parameters.
        /// </summary>
        /// <param name="value">The value of the CID</param>
        /// <exception cref="ArgumentNullException">Thrown if the provided value is null or empty.</exception>
        [JsonConstructor]
        public AtCid(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentNullException(nameof(value));
            }

            Value = value;
        }

        /// <summary>
        /// Gets the value of the CID.
        /// </summary>
        /// <value>
        ///The Content Identifier.
        /// </value>
        [JsonPropertyName("cid")]
        public string Value { get; }

        /// <summary>
        /// Converts the CID to its equivalent string representation.
        /// </summary>
        /// <returns>The string representation of the value of this instance.</returns>
        public override string ToString()
        {
            return Value;
        }
    }
}
