// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace idunno.AtProto;

/// <summary>
/// Implements an AT Protocol Handle.
///
/// Handles are a less-permanent identifier for accounts, when compared to <see cref="Did" />s.
/// </summary>
/// <remarks>
/// <para>Note that handles do not begin with an @ sign, that is just how they are typically displayed in applications.</para>
/// <para>See https://atproto.com/specs/handle for further details.</para>
/// </remarks>
[JsonConverter(typeof(Json.HandleConverter))]
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class Handle : AtIdentifier, IEquatable<Handle>
{
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private const int MaximumLength = 253;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private const int MaximumLabelLength = 63;

    private static readonly SearchValues<char> s_labelCharacters =
        SearchValues.Create("-0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz");

    /// <summary>
    /// A regular expression which checks the syntax of a handle.
    /// </summary>
    /// <remarks>
    /// <para>The expression is suitable for client side validation, for example with a <c>RegularExpressionAttribute</c>.
    /// It checks syntax only, and does not limit the overall length of a handle. Use
    /// <see cref="TryParse(string, out Handle?)"/> to validate a handle fully.</para>
    /// </remarks>
    public const string ValidationRegex = @"^([a-zA-Z0-9]([a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?\.)+[a-zA-Z]([a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?$";


    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "AT Proto standards normalize to lower case.")]
    private Handle(string s, bool validate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(s);

        // Validate before normalizing, as lower casing can map some non-ASCII characters, such as the Kelvin sign, to ASCII letters.
        if (validate)
        {
            Validate(s, true);
        }

        Value = s.ToLowerInvariant();
    }

    /// <summary>
    /// Creates a new instance of <see cref="Handle"/> from the specified string.
    /// </summary>
    /// <param name="s">The string to create a handle from.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="s"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="s"/> is empty or whitespace, or does not validate as a handle.</exception>
    /// <remarks>
    /// <para>Note that handles do not begin with an @ sign, that is just how they are typically displayed in applications.</para>
    /// </remarks>
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
            return !Equals(Invalid);
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
    /// <returns>A <see cref="Handle"/> created from the specified string.</returns>
    /// <remarks>
    /// <para>Note that handles do not begin with an @ sign, that is just how they are typically displayed in applications.</para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Handle(string s) => new(s);

    /// <summary>
    /// Creates a <see cref="Handle"/> from the specified string.
    /// </summary>
    /// <param name="s">The string to convert.</param>
    /// <returns>A <see cref="Handle"/> created from the specified string.</returns>
    /// <remarks>
    /// <para>Note that handles do not begin with an @ sign, that is just how they are typically displayed in applications.</para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static new Handle FromString(string s) => s;

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
    /// <see langword="true"/> if this <see cref="Handle"/> and the specified <paramref name="obj"/>> refer to the same object,
    /// this Handle and the specified obj are both the same type of object and those objects are equal,
    /// or if this Handle and the specified obj are both <see langword="null"/>, otherwise, <see langword="false"/>.
    /// </returns>
    public override bool Equals(object? obj) => Equals(obj as Handle);

    /// <summary>
    /// Indicates where this <see cref="Handle"/> equals another."/>
    /// </summary>
    /// <param name="other">A <see cref="Handle"/> or <see langword="null"/> to compare to this <see cref="Handle"/>.</param>
    /// <returns>
    /// <see langword="true"/> if this <see cref="Handle"/> and the specified <paramref name="other"/>> refer to the same object,
    /// this Handle and the specified obj are both the same type of object and those objects are equal,
    /// or if this Handle and the specified obj are both <see langword="null"/>, otherwise, <see langword="false"/>.
    /// </returns>
    public bool Equals(Handle? other)
    {
        if (other is null)
        {
            return false;
        }

        // Optimization for a common success case.
        if (ReferenceEquals(this, other))
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
    /// <param name="lhs">The first <see cref="Handle"/> to compare, or <see langword="null"/>.</param>
    /// <param name="rhs">The second <see cref="Handle"/> to compare, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the value of <paramref name="lhs"/> is the same as the value of <paramref name="rhs" />; otherwise, <see langword="false"/>.</returns>
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
    /// <param name="lhs">The first <see cref="Handle"/> to compare, or <see langword="null"/>.</param>
    /// <param name="rhs">The second <see cref="Handle"/> to compare, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the value of <paramref name="lhs"/> is different to the value of <paramref name="rhs" />; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(Handle? lhs, Handle? rhs) => !(lhs == rhs);

    /// <summary>
    /// Converts the string representation of an identifier to its <see cref="Handle"/> equivalent.
    /// A return value indicates whether the operation succeeded.
    /// </summary>
    /// <param name="s">A string containing the id to convert.</param>
    /// <param name="result">
    /// When this method returns contains the <see cref="Handle"/> equivalent of the
    /// string contained in s, or <see langword="null"/> if the conversion failed. The conversion fails if the <paramref name="s"/> parameter
    /// is <see langword="null"/> or empty, or is not of the current format. This parameter is passed uninitialized; any value originally
    /// supplied in result will be overwritten.
    /// </param>
    /// <returns><see langword="true"/> if s was converted successfully; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// <para>Note that handles do not begin with an @ sign, that is just how they are typically displayed in applications.</para>
    /// </remarks>
    public static bool TryParse(string s, [NotNullWhen(true)] out Handle? result)
    {
        if (!Validate(s, false))
        {
            result = null;
            return false;
        }

        result = new Handle(s, false);
        return true;
    }

    private static bool Validate(string s, bool throwOnError)
    {
        if (string.IsNullOrWhiteSpace(s))
        {
            if (throwOnError)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(s);
            }

            return false;
        }

        if (s.Length > MaximumLength)
        {
            if (throwOnError)
            {
                throw new ArgumentException($"\"{s}\" length is greater than {MaximumLength}.", nameof(s));
            }

            return false;
        }

        if (s.StartsWith('.') || s.EndsWith('.'))
        {
            if (throwOnError)
            {
                throw new ArgumentException($"\"{s}\" cannot begin or end with '.'.", nameof(s));
            }

            return false;
        }

        if (!s.Contains('.', StringComparison.InvariantCulture))
        {
            if (throwOnError)
            {
                throw new ArgumentException($"\"{s}\" is not a valid hostname.", nameof(s));
            }

            return false;
        }

        if (!IsValidSyntax(s))
        {
            if (throwOnError)
            {
                throw new ArgumentException($"\"{s}\" does not validate as a handle.", nameof(s));
            }

            return false;
        }

        return true;
    }

    // This is a hand written equivalent of ValidationRegex,
    //   ^([a-zA-Z0-9]([a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?\.)+[a-zA-Z]([a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?$
    // from https://atproto.com/specs/handle, except that it does not accept a trailing new line, which $ matches before.
    // A single pass over the characters is several times faster than the regex, even a source generated one, and does not
    // allocate. CanonicalRegexEquivalenceTests checks it accepts exactly what the regex does.
    //
    // A handle is at least two dot separated labels of 1 to 63 letters, digits and hyphens. A label neither starts nor ends
    // with a hyphen, and the last label, the top level domain, starts with a letter.
    internal static bool IsValidSyntax(ReadOnlySpan<char> s)
    {
        int labels = 0;

        while (true)
        {
            int length = s.IndexOf('.');
            bool isTopLevelDomain = length < 0;
            ReadOnlySpan<char> label = isTopLevelDomain ? s : s[..length];

            if (label.IsEmpty ||
                label.Length > MaximumLabelLength ||
                !char.IsAsciiLetterOrDigit(label[0]) ||
                !char.IsAsciiLetterOrDigit(label[^1]) ||
                label.ContainsAnyExcept(s_labelCharacters))
            {
                return false;
            }

            if (isTopLevelDomain)
            {
                return labels >= 1 && char.IsAsciiLetter(label[0]);
            }

            labels++;
            s = s[(length + 1)..];
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private string DebuggerDisplay => ToString();
}