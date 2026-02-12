// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace idunno.AtProto
{
    /// <summary>
    /// Base class for <see cref="Did"/> and <see cref="Handle"/> implementations.
    /// </summary>
    [JsonConverter(typeof(Json.AtIdentifierConverter))]
    public abstract class AtIdentifier
    {
        /// <summary>
        /// Creates a new instance of an <see cref="AtIdentifier"/> from the specified string.
        /// </summary>
        /// <param name="s">The string to create an <see cref="AtIdentifier"/> from.</param>
        /// <returns>An <see cref="AtIdentifier" /> from the specified string</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="s"/> is not valid for an <see cref="AtIdentifier"/>.</exception>
        public static AtIdentifier Create(string s)
        {
            ArgumentException.ThrowIfNullOrEmpty(s);

            if (!TryParse(s, out AtIdentifier? returnValue))
            {
                throw new ArgumentException("{s} is not a valid AtIdentifier", nameof(s));
            }

            return returnValue;
        }

        /// <summary>
        /// Converts the string representation of an identifier to its <see cref="Did"/> or <see cref="Handle"/> equivalent.
        /// A return value indicates whether the operation succeeded.
        /// </summary>
        /// <param name="s">A string containing the id to convert.</param>
        /// <param name="result">
        /// When this method returns contains the <see cref="AtIdentifier"/> equivalent of the
        /// string contained in s, or <see langword="null"/> if the conversion failed. The conversion fails if the <paramref name="s"/> parameter
        /// is <see langword="null"/> or empty, or is not of the current format. This parameter is passed uninitialized; any value originally
        /// supplied in result will be overwritten.
        /// </param>
        /// <returns><see langword="true"/> if s was converted successfully; otherwise, <see langword="false"/>.</returns>
        public static bool TryParse(string s, [NotNullWhen(true)] out AtIdentifier? result)
        {
            if (!string.IsNullOrEmpty(s))
            {
                if (Did.TryParse(s, out Did? did))
                {
                    result = did;
                    return true;
                }

                if (Handle.TryParse(s, out Handle? handle))
                {
                    result = handle;
                    return true;
                }
            }

            result = null;
            return false;
        }

        /// <summary>
        /// Converts the <see cref="AtIdentifier"/> to its equivalent string representation.
        /// </summary>
        /// <returns>The string representation of the value of this instance.</returns>
        public override string ToString() => Value;

        /// <summary>
        /// Implicitly converts <see cref="AtIdentifier"/> to its equivalent string representation.
        /// </summary>
        /// <param name="atIdentifier">The <see cref="AtIdentifier"/> to convert to a string.</param>
        /// <returns>The string representation of the <see cref="AtIdentifier"/>.</returns>
        public static implicit operator string?(AtIdentifier atIdentifier) => atIdentifier?.ToString() ?? null;

        /// <summary>
        /// Creates a <see cref="AtIdentifier"/> from the specified string.
        /// </summary>
        /// <param name="s">The string to convert.</param>
        /// <returns>An <see cref="AtIdentifier"/> created from <paramref name="s"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator AtIdentifier(string s) => Create(s);

        /// <summary>
        /// Creates a <see cref="AtIdentifier"/> from the specified string.
        /// </summary>
        /// <param name="s">The string to convert.</param>
        /// <returns>An <see cref="AtIdentifier"/> created from <paramref name="s"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AtIdentifier FromString(string s) => s;

        /// <summary>
        /// Returns the hash code for this <see cref="AtIdentifier"/>.
        /// </summary>
        /// <returns>The hash code for this <see cref="AtIdentifier"/>.</returns>
        public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);

        /// <summary>
        /// Determines whether the specified object is equal to the current object.
        /// </summary>
        /// <param name="obj">The object to compare with the current object.</param>
        /// <returns><see langword="true"/> if the specified object is equal to the current object; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object? obj) => obj is AtIdentifier identifier && Value == identifier.Value;

        /// <summary>
        /// Gets the underlying value of the <see cref="AtIdentifier"/>/.
        /// </summary>
        public abstract string Value
        {
            get;
        }
    }
}
