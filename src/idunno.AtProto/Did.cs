// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace idunno.AtProto
{
    /// <summary>
    /// Creates a new instance of a Decentralized Identifier.
    /// 
    /// DIDs are a globally unique identifier enabling verification and persistence all without the use of a centralized registry.
    /// 
    /// </summary>
    /// <remarks>
    /// See https://atproto.com/specs/did for further details.
    /// </remarks>
    public sealed class Did : AtIdentifier
    {
        private const string DidPrefix = "did:";

        /// <summary>
        /// Creates a new instance of <see cref="Did"/> using the specified <paramref name="value"/> as the identifier.
        /// </summary>
        /// <param name="value"></param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="value"/> is null or empty.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="value"/> does not begin with "did:".</exception>
        [JsonConstructor]
        public Did(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (!value.StartsWith(DidPrefix, StringComparison.InvariantCulture))
            {
                throw new ArgumentException($"\"{value}\" is not a valid DID", (nameof(value)));
            }

            Value = value;
        }

        /// <summary>
        /// Gets the value of the DID.
        /// </summary>
        [JsonPropertyName("did")]
        public string Value { get; }

        /// <summary>
        /// Converts the DID to its equivalent string representation.
        /// </summary>
        /// <returns>The string representation of the value of this instance.</returns>
        public override string ToString()
        {
            return Value;
        }

        /// <summary>
        /// Converts the string representation of an identifier to its <see cref="Did"/> equivalent.
        /// A return value indicates whether the operation succeeded.
        /// </summary>
        /// <param name="s">A string containing the did to convert.</param>
        /// <param name="result">
        /// When this method returns contains the <see cref="Did"/> equivalent of the
        /// string contained in s, or null if the conversion failed. The conversion fails if the <paramref name="s"/> parameter
        /// is null or empty, or is not of the current format. This parameter is passed uninitialized; any value originally
        /// supplied in result will be overwritten.
        /// </param>
        /// <returns>true if s was converted successfully; otherwise, false.</returns>
        public static bool TryParse(string value, out Did? did)
        {
            if (string.IsNullOrEmpty(value) ||
                !value.StartsWith(DidPrefix, StringComparison.InvariantCulture))
            {
                did = default;
                return false;
            }
            else
            {
                did = new Did(value);
                return true;
            }
        }
    }
}
