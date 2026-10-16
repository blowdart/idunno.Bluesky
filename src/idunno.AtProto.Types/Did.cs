// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace idunno.AtProto;

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
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed partial class Did : AtIdentifier, IEquatable<Did>
{
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private const string DidPrefix = "did:";

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private const int MaximumLength = 2048;

    /// <summary>
    /// A regular expression which checks the syntax of a DID.
    /// </summary>
    /// <remarks>
    /// <para>The expression is suitable for client side validation, for example with a <c>RegularExpressionAttribute</c>.
    /// It checks syntax only, and does not limit the length of the DID. Use <see cref="TryParse(string, out Did?)"/> to
    /// validate a DID fully.</para>
    /// </remarks>
    public const string ValidationRegex = @"^did:[a-z0-9]+(?::(?:%[A-F0-9]{2}|[a-zA-Z0-9._-]+)*)+(?<!:)$";

    private Did(string value, string method)
    {
        Value = value;
        Method = method;
    }

    /// <summary>
    /// Creates a new instance of <see cref="Did"/> using the specified <paramref name="s"/> as the identifier.
    /// </summary>
    /// <param name="s">A string to construct the <see cref="Did"/> from.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="s"/> is <see langword="null"/> or empty.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="s"/> does not pass validation.</exception>
    [JsonConstructor]
    public Did(string s)
    {
        Validate(s, throwOnError: true, out int methodEnd);

        Value = s;
        Method = GetMethod(s.AsSpan(DidPrefix.Length, methodEnd - DidPrefix.Length));
    }

    /// <summary>
    /// Gets the value of the DID.
    /// </summary>
    [JsonPropertyName("did")]
    public override string Value { get; }

    /// <summary>
    /// Gets the method of this <see cref="Did" />.
    /// </summary>
    /// <remarks><para>AT Proto currently supports two methods, web and plc.</para></remarks>
    [JsonIgnore]
    public string Method { get; }

    /// <summary>
    /// Returns the hash code for this <see cref="Did"/>.
    /// </summary>
    /// <returns>The hash code for this <see cref="Did"/>.</returns>
    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);

    /// <summary>
    /// Indicates where an object is equal to this <see cref="Did"/>.
    /// </summary>
    /// <param name="obj">An object to compare to this <see cref="Did"/>.</param>
    /// <returns>
    /// <see langword="true"/> if this <see cref="Did"/> and the specified <paramref name="obj"/> refer to the same object,
    /// this Did and the specified obj are both the same type of object and those objects are equal,
    /// or if this Did and the specified obj are both <see langword="null"/>, otherwise, <see langword="false"/>.
    /// </returns>
    public override bool Equals(object? obj) => Equals(obj as Did);

    /// <summary>
    /// Indicates where this <see cref="Did"/> equals another.
    /// </summary>
    /// <param name="other">A <see cref="Did"/> or <see langword="null"/> to compare to this <see cref="Did"/>.</param>
    /// <returns>
    /// <see langword="true"/> if this <see cref="Did"/> and the specified <paramref name="other"/> refer to the same object,
    /// this Did and the specified obj are both the same type of object and those objects are equal,
    /// or if this Did and the specified obj are both <see langword="null"/>, otherwise, <see langword="false"/>.
    /// </returns>
    public bool Equals(Did? other)
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
    /// <returns>A <see cref="Did"/> created from <paramref name="s"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Did(string s) => new(s);

    /// <summary>
    /// Creates a Did from the specified string.
    /// </summary>
    /// <param name="s">The string to convert.</param>
    /// <returns>A <see cref="Did"/> created from <paramref name="s"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static new Did FromString(string s) => s;

    /// <summary>
    /// Converts the specified did to a string.
    /// </summary>
    /// <param name="d">The Did to convert.</param>
    /// <returns>A string representation of the Did.</returns>
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
    /// <param name="lhs">The first <see cref="Did"/> to compare, or <see langword="null"/>.</param>
    /// <param name="rhs">The second <see cref="Did"/> to compare, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the value of <paramref name="lhs"/> is the same as the value of <paramref name="rhs" />; otherwise, <see langword="false"/>.</returns>
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
    /// Determines whether two specified <see cref="Did"/>s do not have same value.
    /// </summary>
    /// <param name="lhs">The first <see cref="Did"/> to compare, or <see langword="null"/>.</param>
    /// <param name="rhs">The second <see cref="Did"/> to compare, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the value of <paramref name="lhs"/> is different to the value of <paramref name="rhs" />; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(Did? lhs, Did? rhs) => !(lhs == rhs);

    /// <summary>
    /// Converts the string representation of an identifier to its <see cref="Did"/> equivalent.
    /// A return value indicates whether the operation succeeded.
    /// </summary>
    /// <param name="s">A string containing the did to convert.</param>
    /// <param name="result">
    /// When this method returns contains the <see cref="Did"/> equivalent of the
    /// string contained in <paramref name="s"/>, or <see langword="null"/> if the conversion failed. The conversion fails if the <paramref name="s"/> parameter
    /// is <see langword="null"/> or empty, or is not of the current format. This parameter is passed uninitialized; any value originally
    /// supplied in result will be overwritten.
    /// </param>
    /// <returns><see langword="true"/> if <paramref name="s"/> was converted successfully; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(string s, [NotNullWhen(true)] out Did? result)
    {
        return Parse(s, false, out result);
    }

    internal static bool Parse(string s, bool throwOnError, out Did? result)
    {
        if (!Validate(s, throwOnError, out int methodEnd))
        {
            result = null;
            return false;
        }

        result = new Did(s, GetMethod(s.AsSpan(DidPrefix.Length, methodEnd - DidPrefix.Length)));
        return true;
    }

    private static bool Validate(string s, bool throwOnError, out int methodEnd)
    {
        methodEnd = 0;

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

        if (!IsValidSyntax(s, out methodEnd))
        {
            if (throwOnError)
            {
                throw new ArgumentException($"\"{s}\" is not a valid DID", nameof(s));
            }

            return false;
        }

        return true;
    }

    // This is a hand written equivalent of ValidationRegex,
    //   ^did:[a-z0-9]+(?::(?:%[A-F0-9]{2}|[a-zA-Z0-9._-]+)*)+(?<!:)$
    // from https://atproto.com/specs/did, except that it does not accept a trailing new line, which $ matches before.
    // A single pass over the characters is several times faster than the regex, even a source generated one, and does
    // not allocate. CanonicalRegexEquivalenceTests checks it accepts exactly what the regex does.
    internal static bool IsValidSyntax(ReadOnlySpan<char> s, out int methodEnd)
    {
        methodEnd = 0;

        if (!s.StartsWith(DidPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        // The method, [a-z0-9]+, followed by a colon.
        int i = DidPrefix.Length;
        while (i < s.Length && (char.IsAsciiLetterLower(s[i]) || char.IsAsciiDigit(s[i])))
        {
            i++;
        }

        if (i == DidPrefix.Length || i >= s.Length || s[i] != ':' || s[^1] == ':')
        {
            return false;
        }

        methodEnd = i;

        // The method specific identifier, made of [a-zA-Z0-9._:-] and %XX escapes with upper case hex digits.
        i++;
        while (i < s.Length)
        {
            char c = s[i];
            if (c == '%')
            {
                if (i + 2 >= s.Length || !IsUpperHexDigit(s[i + 1]) || !IsUpperHexDigit(s[i + 2]))
                {
                    return false;
                }

                i += 3;
            }
            else if (char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or ':' or '-')
            {
                i++;
            }
            else
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsUpperHexDigit(char c) => char.IsAsciiDigit(c) || c is >= 'A' and <= 'F';

    // The method is a slice of the DID. The two methods AT Proto supports are returned as constants, so they are not
    // allocated for every DID.
    private static string GetMethod(ReadOnlySpan<char> method) => method switch
    {
        "plc" => "plc",
        "web" => "web",
        _ => new string(method)
    };

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private string DebuggerDisplay => ToString();
}