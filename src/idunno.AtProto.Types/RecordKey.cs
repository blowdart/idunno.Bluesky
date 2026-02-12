// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace idunno.AtProto
{
    /// <summary>
    /// A reference to an individual record withing a collection in an atproto repository.
    /// </summary>
    [JsonConverter(typeof(Json.RecordKeyConverter))]
    public sealed partial class RecordKey : IEquatable<RecordKey>
    {
        [GeneratedRegex(@"^[a-zA-Z0-9_~.:-]{1,512}$", RegexOptions.CultureInvariant, 5000)]
        private static partial Regex s_recordKeyValidationRegex();

        private RecordKey(string s, bool validate)
        {
            if (validate)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(s);
                if (Parse(s, true, out _))
                {
                    Value = s;
                }
                else
                {
                    throw new NsidFormatException($"{s} is not a valid nsid.");
                }
            }
            else
            {
                Value = s;
            }
        }

        /// <summary>
        /// Creates a new instance of <see cref="RecordKey"/> from the specified string.
        /// </summary>
        /// <param name="s">The string to create the <see cref="RecordKey"/> from.</param>
        public RecordKey(string s) : this(s, true)
        {
        }

        /// <summary>
        /// Gets the value of this <see cref="RecordKey"/>.
        /// </summary>
        public string Value { get; init; }

        /// <summary>
        /// Returns a string representation of this <see cref="RecordKey"/>.
        /// </summary>
        /// <returns>The string representation of this <see cref="RecordKey"/>.</returns>
        public override string ToString()
        {
            return Value;
        }

        /// <summary>
        /// Creates a RecordKey from the specified string.
        /// </summary>
        /// <param name="s">The string to convert.</param>
        /// <returns>A <see cref="RecordKey"/> created from <paramref name="s"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator RecordKey(string s) => new(s);

        /// <summary>
        /// Creates a RecordKey from the specified string.
        /// </summary>
        /// <param name="s">The string to convert.</param>
        /// <returns>A <see cref="RecordKey"/> created from <paramref name="s"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RecordKey FromString(string s) => s;

        /// <summary>
        /// Returns the hash code for this <see cref="RecordKey"/>.
        /// </summary>
        /// <returns>The hash code for this <see cref="RecordKey"/>.</returns>
        public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);

        /// <summary>
        /// Indicates where an object is equal to this <see cref="RecordKey"/>."/>
        /// </summary>
        /// <param name="obj">An object to compare to this <see cref="RecordKey"/>.</param>
        /// <returns>
        /// <see langword="true"/> if this <see cref="RecordKey"/> and the specified <paramref name="obj"/>> refer to the same object,
        /// this RecordKey and the specified obj are both the same type of object and those objects are equal,
        /// or if this RecordKey and the specified obj are both <see langword="null"/>, otherwise, <see langword="false"/>.
        /// </returns>
        public override bool Equals(object? obj) => Equals(obj as RecordKey);

        /// <summary>
        /// Indicates where this <see cref="RecordKey"/> equals another."/>
        /// </summary>
        /// <param name="other">A <see cref="RecordKey"/> or <see langword="null"/> to compare to this <see cref="RecordKey"/>.</param>
        /// <returns>
        /// <see langword="true"/> if this <see cref="RecordKey"/> and the specified <paramref name="other"/>> refer to the same object,
        /// this RecordKey and the specified obj are both the same type of object and those objects are equal,
        /// or if this RecordKey and the specified obj are both <see langword="null"/>, otherwise, <see langword="false"/>.
        /// </returns>
        public bool Equals(RecordKey? other)
        {
            if (other is null)
            {
                return false;
            }

            if (Object.ReferenceEquals(this, other))
            {
                return true;
            }

            if (GetType() != other.GetType())
            {
                return false;
            }

            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        /// <summary>
        /// Determines whether two specified <see cref="RecordKey"/>s the same value."/>
        /// </summary>
        /// <param name="lhs">The first <see cref="RecordKey"/> to compare, or <see langword="null"/>.</param>
        /// <param name="rhs">The second <see cref="RecordKey"/> to compare, or <see langword="null"/>.</param>
        /// <returns><see langword="true"/> if the value of <paramref name="lhs"/> is the same as the value of <paramref name="rhs" />; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(RecordKey? lhs, RecordKey? rhs)
        {
            if (lhs is null)
            {
                if (rhs is null)
                {
                    return true;
                }

                return false;
            }

            return lhs.Equals(rhs);
        }

        /// <summary>
        /// Determines whether two specified <see cref="RecordKey"/>s dot not have same value."/>
        /// </summary>
        /// <param name="lhs">The first <see cref="RecordKey"/> to compare, or <see langword="null"/>.</param>
        /// <param name="rhs">The second <see cref="RecordKey"/> to compare, or <see langword="null"/>.</param>
        /// <returns><see langword="true"/> if the value of <paramref name="lhs"/> is different to the value of <paramref name="rhs" />; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(RecordKey? lhs, RecordKey? rhs) => !(lhs == rhs);

        /// <summary>
        /// Tries to converts a string to an <see cref="RecordKey"/>.
        /// A return value indicates whether the operation succeeded.
        /// </summary>
        /// <param name="s">A string containing the id to convert.</param>
        /// <param name="result">
        /// When this method returns contains the <see cref="RecordKey"/> equivalent of the
        /// string contained in s, or <see langword="null"/> if the conversion failed. The conversion fails if the <paramref name="s"/> parameter
        /// is <see langword="null"/> or empty, or is not of the current format. This parameter is passed uninitialized; any value originally
        /// supplied in result will be overwritten.
        /// </param>
        /// <returns><see langword="true"/> if <paramref name="s"/> was converted successfully; otherwise, <see langword="false"/>.</returns>
        public static bool TryParse(string s, out RecordKey? result)
        {
            if (string.IsNullOrEmpty(s))
            {
                result = null;
                return false;
            }

            return Parse(s, false, out result);
        }

        internal static bool Parse(string s, bool throwOnError, out RecordKey? result)
        {
            result = null;

            if (s.Length > 512 || s.Length < 1)
            {
                if (throwOnError)
                {
                    throw new RecordKeyFormatException("Record key length must be between 1 and 512 characters");
                }
                else
                {
                    result = null;
                    return false;
                }
            }

            if (!s_recordKeyValidationRegex().IsMatch(s))
            {
                if (throwOnError)
                {
                    throw new RecordKeyFormatException("Record key syntax is invalid.");
                }
                else
                {
                    result = null;
                    return false;
                }
            }

            if (s == "." || s == "..")
            {
                if (throwOnError)
                {
                    throw new RecordKeyFormatException("Record key cannot be \".\" or \"..\".");
                }
                else
                {
                    result = null;
                    return false;
                }
            }

            result = new RecordKey(s, false);
            return true;
        }
    }
}
