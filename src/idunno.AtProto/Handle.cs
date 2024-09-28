// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

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
    /// See https://atproto.com/specs/handle for further details.
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
        public string Value { get; }

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
        public override string ToString()
        {
            return Value;
        }

        public override int GetHashCode() => Value.GetHashCode(StringComparison.OrdinalIgnoreCase);

        public override bool Equals(object? obj) => Equals(obj as Handle);

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
        public static bool TryParse(string s, out Handle? handle)
        {
            return Parse(s, false, out handle);
        }

        private static bool Parse(string s, bool throwOnError, out Handle? handle)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                if (throwOnError)
                {
                    ArgumentException.ThrowIfNullOrWhiteSpace(s);
                }
                else
                {
                    handle = null;
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
                    handle = null;
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
                    handle = null;
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
                    handle = null;
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
                    handle = null;
                    return false;
                }
            }

            handle = new Handle(s, false);
            return true;
        }
    }
}
