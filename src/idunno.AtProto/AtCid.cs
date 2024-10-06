// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Xml.Linq;

namespace idunno.AtProto
{
    /// <summary>
    /// Provides an object representation of an Content Identifier (CID).
    /// </summary>
    /// <remarks>
    /// <para>See https://github.com/multiformats/cid for specification.</para>
    /// </remarks>
    [JsonConverter(typeof(Json.AtCidConverter))]
    public sealed class AtCid : IEquatable<AtCid>
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

        /// <summary>
        /// Creates a <see cref="AtCid"/> from the specified string.
        /// </summary>
        /// <param name="s">The string to convert.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator AtCid(string s) => new(s);

        /// <summary>
        /// Creates a <see cref="AtCid"/> from the specified string.
        /// </summary>
        /// <param name="s">The string to convert.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AtCid FromString(string s) => s;

        /// <summary>
        /// Returns the hash code for this <see cref="AtCid"/>.
        /// </summary>
        /// <returns>The hash code for this <see cref="AtCid"/>.</returns>
        public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);

        /// <summary>
        /// Indicates where an object is equal to this <see cref="AtCid"/>."/>
        /// </summary>
        /// <param name="obj">An object to compare to this <see cref="AtCid"/>.</param>
        /// <returns>
        /// true if this <see cref="AtCid"/> and the specified <paramref name="obj"/>> refer to the same object,
        /// this AtCid and the specified obj are both the same type of object and those objects are equal,
        /// or if this AtCid and the specified obj are both null, otherwise, false.
        /// </returns>
        public override bool Equals(object? obj) => Equals(obj as AtCid);

        /// <summary>
        /// Indicates where this <see cref="AtCid"/> equals another."/>
        /// </summary>
        /// <param name="other">A <see cref="AtCid"/> or null to compare to this <see cref="AtCid"/>.</param>
        /// <returns>
        /// true if this <see cref="AtCid"/> and the specified <paramref name="other"/>> refer to the same object,
        /// this AtCidAtCid and the specified obj are both the same type of object and those objects are equal,
        /// or if this Nsid and the specified obj are both null, otherwise, false.
        /// </returns>
        public bool Equals(AtCid? other)
        {
            if (other is null)
            {
                return false;
            }

            // Optimization for a common success case.
            if (Object.ReferenceEquals(this, other))
            {
                return true;
            }

            // If run-time types are not exactly the same, return false.
            if (GetType() != other.GetType())
            {
                return false;
            }

            // Return true if the fields match.
            return (string.Equals(Value, other.Value, StringComparison.Ordinal));
        }
    }
}
