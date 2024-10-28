// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace idunno.AtProto
{
    /// <summary>
    /// Creates a new instance of a Decentralized Identifier.
    /// 
    /// DIDs are a globally unique identifier enabling verification and persistence all without the use of a centralized registry.
    /// 
    /// </summary>
    /// <remarks>
    /// <para>See https://atproto.com/specs/did for further details on how ATProto uses DIDs.</para>
    /// </remarks>
    [JsonConverter(typeof(Json.DidConverter))]
    public sealed class Did : AtIdentifier, IEquatable<Did>
    {
        private const string DidPrefix = "did:";

        private const string InvalidMethod = "INVALID";

        private const int MaximumLength = 2048;

        private static readonly Regex s_validationRegex =
            new (@"^did:[a-z]+:[a-zA-Z0-9._:%-]*[a-zA-Z0-9._-]$", RegexOptions.CultureInvariant, new TimeSpan(0, 0, 0, 2, 5));

        private Did(string s, bool validate)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(s);

            if (validate)
            {
                if (Parse(s, validate, out Did? did))
                {
                    Value = s;
                    Method = did!.Method;
                }
                else
                {
                    Value = string.Empty;
                    Method = InvalidMethod;
                }
            }
            else
            {
                Value = s;
                Method = GetMethodFromString(s);
            }
        }

        /// <summary>
        /// Creates a new instance of <see cref="Did"/> using the specified <paramref name="s"/> as the identifier.
        /// </summary>
        /// <param name="s"></param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="s"/> is null or empty.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="s"/> does not pass validation.</exception>
        [JsonConstructor]
        public Did(string s) : this(s, true)
        {
        }

        /// <summary>
        /// Gets the value of the DID.
        /// </summary>
        [JsonPropertyName("did")]
        public override string Value { get; } = string.Empty;

        /// <summary>
        /// Gets the method of this <see cref="Did" />.
        /// </summary>
        /// <remarks><para>AT Proto currently supports two methods, web and plc.</para></remarks>
        [JsonIgnore]
        public string Method { get; } = string.Empty;

        /// <summary>
        /// Returns the hash code for this <see cref="Did"/>.
        /// </summary>
        /// <returns>The hash code for this <see cref="Did"/>.</returns>
        public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);

        /// <summary>
        /// Indicates where an object is equal to this <see cref="Did"/>."/>
        /// </summary>
        /// <param name="obj">An object to compare to this <see cref="Did"/>.</param>
        /// <returns>
        /// true if this <see cref="Did"/> and the specified <paramref name="obj"/>> refer to the same object,
        /// this Did and the specified obj are both the same type of object and those objects are equal,
        /// or if this Did and the specified obj are both null, otherwise, false.
        /// </returns>
        public override bool Equals(object? obj) => Equals(obj as Did);

        /// <summary>
        /// Indicates where this <see cref="Did"/> equals another."/>
        /// </summary>
        /// <param name="other">A <see cref="Did"/> or null to compare to this <see cref="Did"/>.</param>
        /// <returns>
        /// true if this <see cref="Did"/> and the specified <paramref name="other"/>> refer to the same object,
        /// this Did and the specified obj are both the same type of object and those objects are equal,
        /// or if this Did and the specified obj are both null, otherwise, false.
        /// </returns>
        public bool Equals(Did? other)
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
            return (string.Equals(Value, other.Value, StringComparison.Ordinal) &&
                    string.Equals(Method, other.Method, StringComparison.Ordinal));
        }

        /// <summary>
        /// Converts the DID to its equivalent string representation.
        /// </summary>
        /// <returns>The string representation of the value of this instance.</returns>
        public override string ToString() => Value;

        /// <summary>
        /// Creates a Did from the specified string.
        /// </summary>
        /// <param name="s">The string to convert.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Did(string s) => new(s);

        /// <summary>
        /// Creates a Did from the specified string.
        /// </summary>
        /// <param name="s">The string to convert.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Did FromString(string s) => s;

        public static implicit operator string(Did d)
        {
            if (d is null)
            {
                return string.Empty;
            }
            else
            {
                return d.ToString();
            }
        }

        /// <summary>
        /// Determines whether two specified <see cref="Did"/>s the same value."/>
        /// </summary>
        /// <param name="lhs">The first <see cref="Did"/> to compare, or null.</param>
        /// <param name="rhs">The second <see cref="Did"/> to compare, or null.</param>
        /// <returns>true if the value of <paramref name="lhs"/> is the same as the value of <paramref name="rhs" />; otherwise, false.</returns>
        public static bool operator ==(Did? lhs, Did? rhs)
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
        /// Determines whether two specified <see cref="Did"/>s dot not have same value."/>
        /// </summary>
        /// <param name="lhs">The first <see cref="Did"/> to compare, or null.</param>
        /// <param name="rhs">The second <see cref="Did"/> to compare, or null.</param>
        /// <returns>true if the value of <paramref name="lhs"/> is different to the value of <paramref name="rhs" />; otherwise, false.</returns>
        public static bool operator !=(Did? lhs, Did? rhs) => !(lhs == rhs);

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
        public static bool TryParse(string s, [NotNullWhen(true)] out Did? result)
        {
            return Parse(s, false, out result);
        }

        internal static bool Parse(string s, bool throwOnError, out Did? result)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                if (throwOnError)
                {
                    ArgumentNullException.ThrowIfNullOrWhiteSpace(s);
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

            if (!s.StartsWith(DidPrefix, StringComparison.InvariantCulture))
            {
                if (throwOnError)
                {
                    throw new ArgumentException($"\"{s}\" is not a valid DID", nameof(s));
                }
                else
                {
                    result = null;
                    return false;
                }
            }

            if (!s_validationRegex.Match(s).Success)
            {
                if (throwOnError)
                {
                    throw new ArgumentException($"\"{s}\" is not a valid DID", nameof(s));
                }
                else
                {
                    result = null;
                    return false;
                }
            }

            result = new Did(s, false);
            return true;
        }

        private static string GetMethodFromString(string s)
        {
            string[] segments = s.Split(':');

            if (segments.Length == 3)
            {
                return segments[1];
            }
            else
            {
                return "INVALID";
            }
        }
    }
}
