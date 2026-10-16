// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Buffers;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace idunno.AtProto;

/// <summary>
/// A class representing a namespace identifier.
/// </summary>
/// <remarks>
/// <para>See https://atproto.com/specs/nsid for details.</para>
/// </remarks>
[JsonConverter(typeof(Json.NsidConverter))]
[TypeConverter(typeof(NsidTypeConverter))]
public sealed partial class Nsid : IEquatable<Nsid>
{
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private readonly string _value;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private const int MaximumLength = 253 + 1 + 63;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private const int MaximumSegmentLength = 63;

    private static readonly SearchValues<char> s_nameCharacters =
        SearchValues.Create("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789");

    private static readonly SearchValues<char> s_segmentCharacters =
        SearchValues.Create("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-");

    /// <summary>
    /// A regular expression which checks the syntax of an NSID.
    /// </summary>
    /// <remarks>
    /// <para>The expression is suitable for client side validation, for example with a <c>RegularExpressionAttribute</c>.
    /// It checks syntax only, and does not limit the length of the NSID. Use <see cref="TryParse(string, out Nsid?)"/> to
    /// validate an NSID fully.</para>
    /// </remarks>
    public const string ValidationRegex = @"^[a-zA-Z]([a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?(\.[a-zA-Z0-9]([a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)+(\.[a-zA-Z]([a-zA-Z0-9]{0,62})?)$";

    private Nsid(string s, bool validate)
    {
        if (validate)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(s);
            if (Validate(s, true))
            {
                _value = s;
            }
            else
            {
                throw new NsidFormatException($"{s} is not a valid nsid.");
            }
        }
        else
        {
            _value = s;
        }
    }

    /// <summary>
    /// Creates a new instance of <see cref="Nsid"/> from the specified string.
    /// </summary>
    /// <param name="s">The string to create an <see cref="Nsid"/> from.</param>
    /// <exception cref="NsidFormatException">Thrown when <paramref name="s"/> is not a valid NSID.</exception>
    [JsonConstructor]
    public Nsid(string s) : this(s, true)
    {
    }

    /// <summary>
    /// Gets the NSID authority for this instance.
    /// </summary>
    /// <value>
    /// The NSID authority for this instance.
    /// </value>
    [JsonIgnore]
    public string Authority => _value[.._value.LastIndexOf('.')];

    /// <summary>
    /// Gets the NSID name for this instance.
    /// </summary>
    /// <value>
    /// The NSID name for this instance.
    /// </value>
    [JsonIgnore]
    public string Name => _value[(_value.LastIndexOf('.') + 1)..];

    /// <summary>
    /// Returns a string representation of the <see cref="Nsid"/> current instance.
    /// </summary>
    /// <returns>a string representation of the <see cref="Nsid"/> current instance.</returns>
    public override string ToString() => _value;

    /// <summary>
    /// Creates an <see cref="Nsid"/> from the specified string.
    /// </summary>
    /// <param name="s">The string to convert.</param>
    /// <returns>An <see cref="Nsid"/> instance created from the specified string.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Nsid(string s) => new(s);

    /// <summary>
    /// Creates an <see cref="Nsid"/> from the specified string.
    /// </summary>
    /// <param name="s">The string to convert.</param>
    /// <returns>An <see cref="Nsid"/> instance created from the specified string.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Nsid FromString(string s) => s;

    /// <summary>
    /// Converts the string representation of an identifier to its <see cref="Nsid"/> equivalent.
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
    public static bool TryParse(string s, out Nsid? result)
    {
        if (string.IsNullOrWhiteSpace(s))
        {
            result = null;
            return false;
        }

        return Parse(s, false, out result);
    }

    /// <summary>
    /// Returns the hash code for this <see cref="Nsid"/>.
    /// </summary>
    /// <returns>The hash code for this <see cref="Nsid"/>.</returns>
    public override int GetHashCode() => _value.GetHashCode(StringComparison.Ordinal);

    /// <summary>
    /// Indicates where an object is equal to this <see cref="Nsid"/>."/>
    /// </summary>
    /// <param name="obj">An object to compare to this <see cref="Nsid"/>.</param>
    /// <returns>
    /// <see langword="true"/> if this <see cref="Nsid"/> and the specified <paramref name="obj"/>> refer to the same object,
    /// this Nsid and the specified obj are both the same type of object and those objects are equal,
    /// or if this Nsid and the specified obj are both <see langword="null"/>, otherwise, <see langword="false"/>.
    /// </returns>
    public override bool Equals(object? obj) => Equals(obj as Nsid);

    /// <summary>
    /// Indicates where this <see cref="Nsid"/> equals another."/>
    /// </summary>
    /// <param name="other">A <see cref="Nsid"/> or <see langword="null"/> to compare to this <see cref="Nsid"/>.</param>
    /// <returns>
    /// <see langword="true"/> if this <see cref="Nsid"/> and the specified <paramref name="other"/>> refer to the same object,
    /// this Nsid and the specified obj are both the same type of object and those objects are equal,
    /// or if this Nsid and the specified obj are both <see langword="null"/>, otherwise, <see langword="false"/>.
    /// </returns>
    public bool Equals(Nsid? other)
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
        return string.Equals(_value, other._value, StringComparison.Ordinal);
    }

    /// <summary>
    /// Determines whether two specified <see cref="Nsid"/>s the same value."/>
    /// </summary>
    /// <param name="lhs">The first <see cref="Nsid"/> to compare, or <see langword="null"/>.</param>
    /// <param name="rhs">The second <see cref="Nsid"/> to compare, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the value of <paramref name="lhs"/> is the same as the value of <paramref name="rhs" />; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(Nsid? lhs, Nsid? rhs)
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
    /// Determines whether two specified <see cref="Nsid"/>s dot not have same value."/>
    /// </summary>
    /// <param name="lhs">The first <see cref="Nsid"/> to compare, or <see langword="null"/>.</param>
    /// <param name="rhs">The second <see cref="Nsid"/> to compare, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the value of <paramref name="lhs"/> is different to the value of <paramref name="rhs" />; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(Nsid? lhs, Nsid? rhs) => !(lhs == rhs);

    internal static bool Parse(string s, bool throwOnError, out Nsid? result)
    {
        if (!Validate(s, throwOnError))
        {
            result = null;
            return false;
        }

        result = new Nsid(s, false);
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
                throw new NsidFormatException($"{s} is too long.");
            }

            return false;
        }

        if (!IsValidSyntax(s))
        {
            if (throwOnError)
            {
                throw new NsidFormatException($"{s} is not a valid nsid.");
            }

            return false;
        }

        return true;
    }

    // This is a hand written equivalent of ValidationRegex,
    //   ^[a-zA-Z]([a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?(\.[a-zA-Z0-9]([a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)+(\.[a-zA-Z]([a-zA-Z0-9]{0,62})?)$
    // from https://atproto.com/specs/nsid, except that it does not accept a trailing new line, which $ matches before.
    // It replaces that regex, a second regex which checked the characters, and splitting the NSID into segments to check
    // each one. A single pass over the characters is several times faster than the regexes, even source generated ones,
    // and does not allocate. CanonicalRegexEquivalenceTests checks it accepts exactly what the regex does.
    //
    // An NSID is at least three dot separated segments of 1 to 63 characters. The first segment starts with a letter. The
    // last segment, the name, is a letter followed by letters and digits. Every other segment is letters, digits and
    // hyphens, and neither starts nor ends with a hyphen.
    internal static bool IsValidSyntax(ReadOnlySpan<char> s)
    {
        int segments = 0;

        while (true)
        {
            int length = s.IndexOf('.');
            bool isName = length < 0;
            ReadOnlySpan<char> segment = isName ? s : s[..length];

            if (segment.IsEmpty || segment.Length > MaximumSegmentLength)
            {
                return false;
            }

            if (isName)
            {
                return segments >= 2 && char.IsAsciiLetter(segment[0]) && !segment.ContainsAnyExcept(s_nameCharacters);
            }

            bool validStart = segments == 0 ? char.IsAsciiLetter(segment[0]) : char.IsAsciiLetterOrDigit(segment[0]);
            if (!validStart || !char.IsAsciiLetterOrDigit(segment[^1]) || segment.ContainsAnyExcept(s_segmentCharacters))
            {
                return false;
            }

            segments++;
            s = s[(length + 1)..];
        }
    }
}