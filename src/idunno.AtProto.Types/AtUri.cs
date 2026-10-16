// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace idunno.AtProto;

/// <summary>
/// Provides an object representation of an AT uniform resource identifier (AT URI) and easy access to the parts of the AT URI.
/// </summary>
/// <remarks>
/// <para>See https://atproto.com/specs/at-uri-scheme</para>
/// <para>For Bluesky a valid form is at-uri = at://{repo (did)}/{collection}/{rkey}</para>
/// </remarks>
[JsonConverter(typeof(Json.AtUriConverter))]
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed partial class AtUri : IEquatable<AtUri>
{
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private const string Protocol = "at";

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private const string ProtocolAndSeparator = "at://";

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private const int MaximumLength = 8 * 1024;

    private static readonly SearchValues<char> s_validCharacters =
        SearchValues.Create("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789._~:@!$&')(*+,;=%/-");

    private string? _value;

    private AtUri(string scheme, AtIdentifier authority, string? path, Nsid? collection, RecordKey? rKey)
    {
        Scheme = scheme;
        Authority = authority;
        AbsolutePath = path;
        Collection = collection;
        RecordKey = rKey;
    }

    /// <summary>
    /// Creates a new instance of the <see cref="AtUri"/> class from <paramref name="s"/>.
    /// </summary>
    /// <param name="s">A string to construct an <see cref="AtUri"/> from.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="s"/> is <see langword="null"/>, empty or whitespace.</exception>
    /// <exception cref="AtUriFormatException">Thrown when <paramref name="s"/> is not a valid AT URI.</exception>
    public AtUri(string s)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(s);

        if (Parse(s, true, out AtIdentifier? authority, out string? absolutePath, out Nsid? collection, out RecordKey? recordKey))
        {
            Scheme = Protocol;
            Authority = authority;
            AbsolutePath = absolutePath;
            Collection = collection;
            RecordKey = recordKey;
        }
        else
        {
            throw new ArgumentException($"{s} is not a valid AT Uri", nameof(s));
        }
    }

    /// <summary>
    /// Gets the scheme name for this <see cref="AtUri"/>.
    ///
    /// This will always be "at" for a valid AtUri.
    /// </summary>
    /// <value>The normalized scheme component for this <see cref="AtUri"/>.</value>
    public string Scheme { get; } = string.Empty;

    /// <summary>
    /// Gets the <see cref="AtIdentifier"/> from this <see cref="AtUri"/>.
    /// </summary>
    public AtIdentifier Authority { get; }

    /// <summary>
    /// Gets the absolute path for this <see cref="AtUri"/>, if it contains an absolute path, otherwise <see langword="null"/>.
    /// </summary>
    public string? AbsolutePath { get; }

    /// <summary>
    /// Gets the <see cref="AtIdentifier"/> from this <see cref="AtUri"/> if the AtUri contains a repo (authority).
    /// </summary>
    [JsonIgnore]
    public AtIdentifier Repo => Authority;

    /// <summary>
    /// Returns the collection segment of the <see cref="AtUri"/> or <see langword="null"/> if the <see cref="AtUri"/> does not contain a collection.
    /// </summary>
    [JsonIgnore]
    public Nsid? Collection { get; }

    /// <summary>
    /// Returns the record key of the AT URI or <see langword="null"/> if the URI does not contain one.
    /// </summary>
    [JsonIgnore]
    public RecordKey? RecordKey { get; }

    /// <summary>
    /// Returns the hash code for this <see cref="AtUri"/>.
    /// </summary>
    /// <returns>The hash code for this <see cref="AtUri"/>.</returns>
    public override int GetHashCode() => (Scheme, Authority, AbsolutePath).GetHashCode();

    /// <summary>
    /// Indicates where an object is equal to this <see cref="AtUri"/>.
    /// </summary>
    /// <param name="obj">An object to compare to this <see cref="AtUri"/>.</param>
    /// <returns>
    /// <see langword="true"/> if this <see cref="AtUri"/> and the specified <paramref name="obj"/>> refer to the same object,
    /// this AtUri and the specified obj are both the same type of object and those objects are equal,
    /// or if this AtUri and the specified obj are both <see langword="null"/>, otherwise, <see langword="false"/>.
    /// </returns>
    public override bool Equals([NotNullWhen(true)] object? obj) => Equals(obj as AtUri);

    /// <summary>
    /// Indicates where this <see cref="AtUri"/> equals another.
    /// </summary>
    /// <param name="other">A <see cref="AtUri"/> or <see langword="null"/> to compare to this <see cref="AtUri"/>.</param>
    /// <returns>
    /// <see langword="true"/> if this <see cref="AtUri"/> and the specified <paramref name="other"/>> refer to the same object,
    /// this AtUri and the specified obj are both the same type of object and those objects are equal,
    /// or if this AtUri and the specified obj are both <see langword="null"/>, otherwise, <see langword="false"/>.
    /// </returns>
    public bool Equals([NotNullWhen(true)] AtUri? other)
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

        if (!Authority.Equals(other.Authority))
        {
            return false;
        }

        return string.Equals(Scheme, other.Scheme, StringComparison.Ordinal) &&
               string.Equals(AbsolutePath, other.AbsolutePath, StringComparison.Ordinal);
    }

    /// <summary>
    /// Determines whether two specified <see cref="AtUri"/>s the same value.
    /// </summary>
    /// <param name="lhs">The first <see cref="AtUri"/> to compare, or <see langword="null"/>.</param>
    /// <param name="rhs">The second <see cref="AtUri"/> to compare, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the value of <paramref name="lhs"/> is the same as the value of <paramref name="rhs" />; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(AtUri? lhs, AtUri? rhs)
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
    /// Determines whether two specified <see cref="AtUri"/>s dot not have same value.
    /// </summary>
    /// <param name="lhs">The first <see cref="AtUri"/> to compare, or <see langword="null"/>.</param>
    /// <param name="rhs">The second <see cref="AtUri"/> to compare, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the value of <paramref name="lhs"/> is different to the value of <paramref name="rhs" />; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(AtUri? lhs, AtUri? rhs) => !(lhs == rhs);

    /// <summary>
    /// Serializes the component parts of the AT URI represented by this instance into a string.
    /// </summary>
    /// <returns>A string representation of the AT URI.</returns>
    /// <remarks>
    /// <para>An <see cref="AtUri"/> cannot be changed once it is created, so the string is built once and then reused.</para>
    /// </remarks>
    public override string ToString() => _value ??= Format();

    private string Format()
    {
        string scheme = string.IsNullOrEmpty(Scheme) ? string.Empty : Scheme + "://";

        return string.Concat(scheme, Authority.ToString(), AbsolutePath);
    }

    /// <summary>
    /// Creates a <see cref="AtUri"/> from the specified string.
    /// </summary>
    /// <param name="s">The string to convert.</param>
    /// <returns>An <see cref="AtUri"/> from the specified string.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator AtUri(string s) => new(s);

    /// <summary>
    /// Creates a <see cref="AtUri"/> from the specified string.
    /// </summary>
    /// <param name="s">The string to convert.</param>
    /// <returns>An <see cref="AtUri"/> from the specified string.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AtUri FromString(string s) => s;

    /// <summary>
    /// Converts the string representation of an identifier to its <see cref="AtUri"/> equivalent.
    /// A return value indicates whether the operation succeeded.
    /// </summary>
    /// <param name="s">A string containing the id to convert.</param>
    /// <param name="result">
    /// When this method returns contains the <see cref="AtUri"/> equivalent of the
    /// string contained in s, or <see langword="null"/> if the conversion failed. The conversion fails if the <paramref name="s"/> parameter
    /// is <see langword="null"/> or empty, or is not of the current format. This parameter is passed uninitialized; any value originally
    /// supplied in result will be overwritten.
    /// </param>
    /// <returns><see langword="true"/> if <paramref name="s"/> was converted successfully; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(string s, [NotNullWhen(true)] out AtUri? result)
    {
        if (string.IsNullOrEmpty(s))
        {
            result = null;
            return false;
        }

        return Parse(s, false, out result);
    }

    private static bool Parse(string s, bool throwOnError, out AtUri? result)
    {
        if (!Parse(s, throwOnError, out AtIdentifier? authority, out string? absolutePath, out Nsid? collection, out RecordKey? recordKey))
        {
            result = null;
            return false;
        }

        result = new AtUri(Protocol, authority, absolutePath, collection, recordKey);
        return true;
    }

    // Validation comes from https://github.com/bluesky-social/atproto/blob/290a7e67b8e6417b00352cc1d54bac006c2f6f93/packages/syntax/src/aturi_validation.ts#L99
    //
    // This used to check s against two regular expressions,
    //   ^[a-zA-Z0-9._~:@!$&')(*+,;=%/-]*$
    //   ^at:\/\/(?<authority>[a-zA-Z0-9._:%-]+)(\/(?<collection>[a-zA-Z0-9-.]+)(\/(?<rkey>[a-zA-Z0-9._~:@!$&%')(*+,;=-]+))?)?(#(?<fragment>\/[a-zA-Z0-9._~:@!$&%')(*+,;=\-[\]/\\]*))?$
    // then split it on '/' and validated the authority twice. It now checks the characters with a single search, and
    // slices the authority, collection and record key out of s once each, validating each with its own type, which is
    // stricter than the regex. That is around ten times faster than the regexes, even source generated ones, and allocates
    // a fifth as much. Unlike the regexes it does not accept a trailing new line, which $ matches before.
    // CanonicalRegexEquivalenceTests checks it accepts exactly what the regex and the component types do together.
    private static bool Parse(
        string s,
        bool throwOnError,
        [NotNullWhen(true)] out AtIdentifier? authority,
        out string? absolutePath,
        out Nsid? collection,
        out RecordKey? recordKey)
    {
        authority = null;
        absolutePath = null;
        collection = null;
        recordKey = null;

        if (s.Length > MaximumLength)
        {
            return Fail(throwOnError, $"{s} is too long.");
        }

        if (!s.StartsWith(ProtocolAndSeparator, StringComparison.Ordinal))
        {
            return Fail(throwOnError, nameof(s) + " has an invalid scheme.");
        }

        if (s.Contains('?', StringComparison.Ordinal))
        {
            return Fail(throwOnError, "AT URIs cannot contain a query part.");
        }

        if (s.Contains('#', StringComparison.Ordinal))
        {
            return Fail(throwOnError, "AT URIs cannot contain a fragment.");
        }

        if (s.AsSpan().ContainsAnyExcept(s_validCharacters))
        {
            return Fail(throwOnError, "AT URIs can only contain ASCII.");
        }

        ReadOnlySpan<char> remaining = s.AsSpan(ProtocolAndSeparator.Length);
        int authorityLength = remaining.IndexOf('/');
        if (authorityLength < 0)
        {
            authorityLength = remaining.Length;
        }

        if (authorityLength == 0)
        {
            return Fail(throwOnError, $"{nameof(s)} contains no authority or path.");
        }

        string authorityValue = s.Substring(ProtocolAndSeparator.Length, authorityLength);

        if (authorityValue.StartsWith("did:", StringComparison.OrdinalIgnoreCase))
        {
            if (!Did.TryParse(authorityValue, out Did? did))
            {
                return Fail(throwOnError, $"{authorityValue} is not a valid DID.");
            }

            authority = did;
        }
        else
        {
            if (!Handle.TryParse(authorityValue, out Handle? handle))
            {
                return Fail(throwOnError, $"{authorityValue} is not a valid handle.");
            }

            authority = handle;
        }

        if (authorityLength == remaining.Length)
        {
            return true;
        }

        absolutePath = s[(ProtocolAndSeparator.Length + authorityLength)..];

        ReadOnlySpan<char> path = remaining[(authorityLength + 1)..];
        int collectionLength = path.IndexOf('/');
        ReadOnlySpan<char> collectionSegment = collectionLength < 0 ? path : path[..collectionLength];

        // Failures in either path segment are reported as an AtUriFormatException, so that every way an AT URI
        // can be malformed is reported the same way. The segment specific exceptions belong to callers parsing
        // a segment in isolation, not to callers parsing a URI.
        if (!Nsid.Parse(new string(collectionSegment), false, out collection))
        {
            return Fail(throwOnError, $"Collection segment, {collectionSegment}, must be a valid NSID.");
        }

        if (collectionLength < 0)
        {
            return true;
        }

        ReadOnlySpan<char> recordKeySegment = path[(collectionLength + 1)..];

        if (recordKeySegment.Contains('/'))
        {
            return Fail(throwOnError, $"{s} has too many segments");
        }

        if (!RecordKey.Parse(new string(recordKeySegment), false, out recordKey))
        {
            return Fail(throwOnError, $"Record key segment, {recordKeySegment}, must be a valid record key.");
        }

        return true;
    }

    private static bool Fail(bool throwOnError, string message) =>
        throwOnError ? throw new AtUriFormatException(message) : false;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private string DebuggerDisplay => ToString();
}