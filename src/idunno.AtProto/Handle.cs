// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace idunno.AtProto
{
    /// <summary>
    /// Implements an AT Protocol Handle.
    ///
    /// Handles are a less-permanent identifier for accounts, when compared to <see cref="Did" />s.
    /// </summary>
    /// <remarks>
    /// <para>See https://atproto.com/specs/handle for further details.</para>
    /// </remarks>
    [JsonConverter(typeof(Json.HandleConverter))]
    public class Handle : AtIdentifier, IEquatable<Handle>
    {
        private const int MaximumLength = 253;

        private static readonly Regex s_validate = new(
            @"^([a-zA-Z0-9]([a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?\.)+[a-zA-Z]([a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?$",
            RegexOptions.None,
            TimeSpan.FromMilliseconds(100));

        private Handle(string s, bool validate)
        {
            Value = string.Empty;

            // Normalize
            s = s.ToLowerInvariant();

            if (validate)
            {
                if (Parse(s, true, out Handle? _))
                {
                    Value = s;
                }
            }
            else
            {
                Value = s;
            }
        }

        /// <summary>
        /// Creates a new instance of <see cref="Handle"/> from the specified string.
        /// </summary>
        /// <param name="s">The string to create a handle from.</param>
        [JsonConstructor]
        public Handle(string s) : this(s, true)
        {
        }

        /// <summary>
        /// The special handle value that can be used by APIs to indicate that there is no bi-directionally valid handle for a given DID.
        /// This handle can not be used in most situations (search queries, API requests, etc).
        /// </summary>
        /// <value>
        /// The special handle value that can be used by APIs to indicate that there is no bi-directionally valid handle for a given DID.
        /// </value>
        [JsonIgnore]
        public static Handle Invalid { get; } = new Handle("handle.invalid", false);

        /// <summary>
        /// Gets the normalized value of the handle.
        /// </summary>
        [JsonPropertyName("handle")]
        public override string Value { get; }

        /// <summary>
        /// Returns a flag indicating if the handle is or is not equal to the reserved <see cref="Invalid"/> handle.
        /// </summary>
        public bool IsValid
        {
            get
            {
                return !Equals(Handle.Invalid);
            }
        }

        /// <summary>
        /// Converts the Handle to its equivalent string representation.
        /// </summary>
        /// <returns>The string representation of the value of this instance.</returns>
        public override string ToString() => Value;

        /// <summary>
        /// Creates a <see cref="Handle"/> from the specified string.
        /// </summary>
        /// <param name="s">The string to convert.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Handle(string s) => new(s);

        /// <summary>
        /// Creates a <see cref="Handle"/> from the specified string.
        /// </summary>
        /// <param name="s">The string to convert.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Handle FromString(string s) => s;

        /// <summary>
        /// Returns the hash code for this <see cref="Handle"/>.
        /// </summary>
        /// <returns>The hash code for this <see cref="Handle"/>.</returns>

        public override int GetHashCode() => Value.GetHashCode(StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Indicates where an object is equal to this <see cref="Handle"/>."/>
        /// </summary>
        /// <param name="obj">An object to compare to this <see cref="Handle"/>.</param>
        /// <returns>
        /// true if this <see cref="Handle"/> and the specified <paramref name="obj"/>> refer to the same object,
        /// this Handle and the specified obj are both the same type of object and those objects are equal,
        /// or if this Handle and the specified obj are both null, otherwise, false.
        /// </returns>
        public override bool Equals(object? obj) => Equals(obj as Handle);

        /// <summary>
        /// Indicates where this <see cref="Handle"/> equals another."/>
        /// </summary>
        /// <param name="other">A <see cref="Handle"/> or null to compare to this <see cref="Handle"/>.</param>
        /// <returns>
        /// true if this <see cref="Handle"/> and the specified <paramref name="other"/>> refer to the same object,
        /// this Handle and the specified obj are both the same type of object and those objects are equal,
        /// or if this Handle and the specified obj are both null, otherwise, false.
        /// </returns>
        public bool Equals(Handle? other)
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
            return (string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Determines whether two specified <see cref="Handle"/>s the same value."/>
        /// </summary>
        /// <param name="lhs">The first <see cref="Handle"/> to compare, or null.</param>
        /// <param name="rhs">The second <see cref="Handle"/> to compare, or null.</param>
        /// <returns>true if the value of <paramref name="lhs"/> is the same as the value of <paramref name="rhs" />; otherwise, false.</returns>
        public static bool operator ==(Handle? lhs, Handle? rhs)
        {
            if (lhs is null)
            {
                if (rhs is null)
                {
                    return true;
                }

                // Only the left side is null.
                return false;
            }
            // Equals handles case of null on right side.
            return lhs.Equals(rhs);
        }

        /// <summary>
        /// Determines whether two specified <see cref="Handle"/>s dot not have same value."/>
        /// </summary>
        /// <param name="lhs">The first <see cref="Handle"/> to compare, or null.</param>
        /// <param name="rhs">The second <see cref="Handle"/> to compare, or null.</param>
        /// <returns>true if the value of <paramref name="lhs"/> is different to the value of <paramref name="rhs" />; otherwise, false.</returns>
        public static bool operator !=(Handle? lhs, Handle? rhs) => !(lhs == rhs);

        /// <summary>
        /// Converts the string representation of an identifier to its <see cref="Handle"/> equivalent.
        /// A return value indicates whether the operation succeeded.
        /// </summary>
        /// <param name="s">A string containing the id to convert.</param>
        /// <param name="result">
        /// When this method returns contains the <see cref="Handle"/> equivalent of the
        /// string contained in s, or null if the conversion failed. The conversion fails if the <paramref name="s"/> parameter
        /// is null or empty, or is not of the current format. This parameter is passed uninitialized; any value originally
        /// supplied in result will be overwritten.
        /// </param>
        /// <returns>true if s was converted successfully; otherwise, false.</returns>
        public static bool TryParse(string s, out Handle? result)
        {
            return Parse(s, false, out result);
        }

        private static bool Parse(string s, bool throwOnError, out Handle? result)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                if (throwOnError)
                {
                    ArgumentException.ThrowIfNullOrWhiteSpace(s);
                }
                else
                {
                    result = null;
                    return false;
                }
            }

            if (s.Length > MaximumLength)
            {
                if (throwOnError)
                {
                    throw new ArgumentException($"\"{s}\" length is greater than {MaximumLength}.", nameof(s));
                }
                else
                {
                    result = null;
                    return false;
                }
            }

            if (s.StartsWith('.') || s.EndsWith('.'))
            {
                if (throwOnError)
                {
                    throw new ArgumentException($"\"{s}\" cannot begin or end with '.'.", nameof(s));
                }
                else
                {
                    result = null;
                    return false;
                }
            }

            if (!s.Contains('.', StringComparison.InvariantCulture))
            {
                if (throwOnError)
                {
                    throw new ArgumentException($"\"{s}\" is not a valid hostname.", nameof(s));
                }
                else
                {
                    result = null;
                    return false;
                }
            }

            if (!s_validate.IsMatch(s))
            {
                if (throwOnError)
                {
                    throw new ArgumentException($"\"{s}\" does not validate as a handle.", nameof(s));
                }
                else
                {
                    result = null;
                    return false;
                }
            }

            result = new Handle(s, false);
            return true;
        }
    }
}
