// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace idunno.AtProto
{
    /// <summary>
    /// Provides an object representation of an Content Identifier (CID).
    /// </summary>
    /// <remarks>
    /// <para>See https://github.com/multiformats/cid for specification.</para>
    /// </remarks>
    [JsonConverter(typeof(Json.CidConverter))]
    public sealed class Cid : IEquatable<Cid>
    {
        /// <summary>
        /// Creates a new instance of a <see cref="Cid"/> class using the specified parameters.
        /// </summary>
        /// <param name="value">The value of the content identifier.</param>
        /// <exception cref="ArgumentNullException">Thrown if the provided value is null or empty.</exception>
        [JsonConstructor]
        public Cid(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentNullException(nameof(value));
            }

            Value = value;
        }

        /// <summary>
        /// Gets the value of the Content Identifier.
        /// </summary>
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
        /// Creates a <see cref="Cid"/> from the specified string.
        /// </summary>
        /// <param name="s">The string to convert.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Cid(string s) => new(s);

        /// <summary>
        /// Creates a <see cref="Cid"/> from the specified string.
        /// </summary>
        /// <param name="s">The string to convert.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Cid FromString(string s) => s;

        /// <summary>
        /// Returns the hash code for this <see cref="Cid"/>.
        /// </summary>
        public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);

        /// <summary>
        /// Indicates where an object is equal to this <see cref="Cid"/>."/>
        /// </summary>
        /// <param name="obj">An object to compare to this <see cref="Cid"/>.</param>
        /// <returns>
        /// true if this <see cref="Cid"/> and the specified <paramref name="obj"/>> refer to the same object,
        /// this Cid and the specified obj are both the same type of object and those objects are equal,
        /// or if this Cid and the specified obj are both null, otherwise, false.
        /// </returns>
        public override bool Equals(object? obj) => Equals(obj as Cid);

        /// <summary>
        /// Indicates where this <see cref="Cid"/> equals another."/>
        /// </summary>
        /// <param name="other">A <see cref="Cid"/> or null to compare to this <see cref="Cid"/>.</param>
        /// <returns>
        /// true if this <see cref="Cid"/> and the specified <paramref name="other"/> refer to the same object,
        /// this Cid and the specified obj are both the same type of object and those objects are equal,
        /// or if this Cid and the specified obj are both null, otherwise, false.
        /// </returns>
        public bool Equals(Cid? other)
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
