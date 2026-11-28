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
/// <para>
/// Permissioned space data is addressed by space URIs, <c>at:space/{spaceDid}/{spaceType}/{skey}</c> for a space and
/// <c>at:space/{spaceDid}/{spaceType}/{skey}/{authorDid}/{collection}/{rkey}</c> for a record in a space.
/// Space URIs are valid wherever an AT URI is accepted. <see cref="Parse(string, IFormatProvider?)"/>,
/// <see cref="TryParse(string, out AtUri?)"/>, <see cref="FromString(string)"/>, the implicit conversion from <see cref="string"/>,
/// and JSON deserialization return a <see cref="SpaceAtUri"/> for a space URI. The <see cref="AtUri(string)"/> constructor
/// cannot return a derived type, so use <see cref="IsSpaceUri"/> or <see cref="TryGetSpaceUri(out SpaceAtUri?)"/> rather than
/// a type check to detect a space URI.
/// </para>
/// <para>
/// Equality is based on the value of the URI and not on its runtime type, so an <see cref="AtUri"/> and a <see cref="SpaceAtUri"/>
/// for the same space URI are equal.
/// </para>
/// <para>See https://github.com/bluesky-social/proposals/tree/main/0016-permissioned-data for the space URI proposal.</para>
/// </remarks>
[JsonConverter(typeof(Json.AtUriConverter))]
[DebuggerDisplay("{DebuggerDisplay,nq}")]
[SuppressMessage("Major Code Smell", "S4035:Classes implementing \"IEquatable<T>\" should be sealed", Justification = "Equality is sealed in AtUri and is based on the URI value, not its runtime type, so SpaceAtUri instances compare correctly with AtUri instances.")]
public partial class AtUri : IEquatable<AtUri>, IParsable<AtUri>
{
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private const string Protocol = "at";

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private const string ProtocolAndSeparator = "at://";

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private const string SpacePrefix = "at:space/";

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    internal const int MaximumLength = 8 * 1024;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private const int SpaceSegmentCount = 3;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private const int SpaceRecordSegmentCount = 6;

    private static readonly SearchValues<char> s_validCharacters =
        SearchValues.Create("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789._~:@!$&')(*+,;=%/-");

    private string? _value;

    /// <summary>
    /// Initializes a new instance of the <see cref="AtUri"/> class from validated components.
    /// </summary>
    /// <param name="components">The validated components of the AT URI.</param>
    /// <remarks>
    /// <para>The components must have come from the parser, or have been validated to the same standard, as no further validation is performed.</para>
    /// </remarks>
    internal AtUri(in AtUriComponents components)
    {
        Scheme = Protocol;
        Authority = components.Authority;
        AbsolutePath = components.AbsolutePath;
        Collection = components.Collection;
        RecordKey = components.RecordKey;
        IsSpaceUri = components.IsSpace;
        SpaceTypeCore = components.SpaceType;
        SpaceKeyCore = components.SpaceKey;
        AuthorDidCore = components.AuthorDid;
    }

    /// <summary>
    /// Creates a new instance of the <see cref="AtUri"/> class from <paramref name="s"/>.
    /// </summary>
    /// <param name="s">A string to construct an <see cref="AtUri"/> from.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="s"/> is <see langword="null"/>, empty or whitespace.</exception>
    /// <exception cref="AtUriFormatException">Thrown when <paramref name="s"/> is not a valid AT URI.</exception>
    /// <remarks>
    /// <para>
    /// A space URI is accepted, but the constructor always creates an <see cref="AtUri"/>, never a <see cref="SpaceAtUri"/>.
    /// Use <see cref="FromString(string)"/> or <see cref="Parse(string, IFormatProvider?)"/> to get a <see cref="SpaceAtUri"/>
    /// for a space URI, or <see cref="TryGetSpaceUri(out SpaceAtUri?)"/> to get one from an existing instance.
    /// </para>
    /// </remarks>
    public AtUri(string s) : this(ParseOrThrow(s))
    {
    }

    /// <summary>
    /// Gets the scheme name for this <see cref="AtUri"/>.
    ///
    /// This will always be "at" for a valid AtUri.
    /// </summary>
    /// <value>The normalized scheme component for this <see cref="AtUri"/>.</value>
    public string Scheme { get; }

    /// <summary>
    /// Gets the <see cref="AtIdentifier"/> from this <see cref="AtUri"/>.
    /// </summary>
    /// <value>
    /// The authority of a public AT URI, which is the repository, or the space authority DID of a space URI.
    /// </value>
    /// <remarks>
    /// <para>
    /// The authority of a space URI identifies the space authority, not a repository. Use <see cref="Repo"/> when
    /// a repository is required.
    /// </para>
    /// </remarks>
    public AtIdentifier Authority { get; }

    /// <summary>
    /// Gets the absolute path for this <see cref="AtUri"/>, if it contains an absolute path, otherwise <see langword="null"/>.
    /// </summary>
    /// <value>
    /// The path following the authority of a public AT URI, or <see langword="null"/> if the URI has no path or is a space URI.
    /// </value>
    /// <remarks>
    /// <para>A space URI, which begins <c>at:space/</c>, has no authority component, and so has no absolute path.</para>
    /// </remarks>
    public string? AbsolutePath { get; }

    /// <summary>
    /// Gets a value that indicates whether this <see cref="AtUri"/> addresses permissioned space data.
    /// </summary>
    /// <value>
    /// <see langword="true"/> if this is a space URI, which begins <c>at:space/</c>; otherwise, <see langword="false"/>.
    /// </value>
    [JsonIgnore]
    [MemberNotNullWhen(false, nameof(Repo))]
    public bool IsSpaceUri { get; }

    /// <summary>
    /// Gets the repository from this <see cref="AtUri"/>.
    /// </summary>
    /// <value>
    /// The repository of a public AT URI, or <see langword="null"/> for a space URI.
    /// </value>
    /// <remarks>
    /// <para>
    /// A record in a space is held in a per-author space repository, identified by both the space and the author, which
    /// cannot be represented by a single <see cref="AtIdentifier"/>. Use <see cref="SpaceAtUri.SpaceDid"/> and
    /// <see cref="SpaceAtUri.AuthorDid"/> for space URIs.
    /// </para>
    /// </remarks>
    [JsonIgnore]
    public AtIdentifier? Repo => IsSpaceUri ? null : Authority;

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
    /// Gets the space type of a space URI, or <see langword="null"/> for a public AT URI.
    /// </summary>
    internal Nsid? SpaceTypeCore { get; }

    /// <summary>
    /// Gets the space key of a space URI, or <see langword="null"/> for a public AT URI.
    /// </summary>
    internal string? SpaceKeyCore { get; }

    /// <summary>
    /// Gets the author DID of a space record URI, or <see langword="null"/> for a space URI without a record, or a public AT URI.
    /// </summary>
    internal Did? AuthorDidCore { get; }

    /// <summary>
    /// Gets a <see cref="SpaceAtUri"/> for this <see cref="AtUri"/>, if it is a space URI.
    /// </summary>
    /// <param name="spaceUri">
    /// When this method returns, contains a <see cref="SpaceAtUri"/> equal to this instance if it is a space URI,
    /// otherwise <see langword="null"/>. This parameter is treated as uninitialized.
    /// </param>
    /// <returns><see langword="true"/> if this <see cref="AtUri"/> is a space URI; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// <para>If this instance is already a <see cref="SpaceAtUri"/> it is returned, otherwise a new <see cref="SpaceAtUri"/> is created.</para>
    /// </remarks>
    [Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
    public bool TryGetSpaceUri([NotNullWhen(true)] out SpaceAtUri? spaceUri)
    {
        spaceUri = this as SpaceAtUri;
        if (spaceUri is not null)
        {
            return true;
        }

        if (!IsSpaceUri)
        {
            spaceUri = null;
            return false;
        }

        spaceUri = new SpaceAtUri(new AtUriComponents(
            IsSpace: true,
            Authority: Authority,
            AbsolutePath: null,
            SpaceType: SpaceTypeCore,
            SpaceKey: SpaceKeyCore,
            AuthorDid: AuthorDidCore,
            Collection: Collection,
            RecordKey: RecordKey));
        return true;
    }

    /// <summary>
    /// Returns the hash code for this <see cref="AtUri"/>.
    /// </summary>
    /// <returns>The hash code for this <see cref="AtUri"/>.</returns>
    /// <remarks>
    /// <para>The hash code is based on the value of the URI, not on its runtime type.</para>
    /// </remarks>
    public sealed override int GetHashCode()
    {
        if (!IsSpaceUri)
        {
            return (Scheme, Authority, AbsolutePath).GetHashCode();
        }

        HashCode hashCode = new();
        hashCode.Add(IsSpaceUri);
        hashCode.Add(Authority);
        hashCode.Add(SpaceTypeCore);
        hashCode.Add(SpaceKeyCore, StringComparer.Ordinal);
        hashCode.Add(AuthorDidCore);
        hashCode.Add(Collection);
        hashCode.Add(RecordKey);
        return hashCode.ToHashCode();
    }

    /// <summary>
    /// Indicates where an object is equal to this <see cref="AtUri"/>.
    /// </summary>
    /// <param name="obj">An object to compare to this <see cref="AtUri"/>.</param>
    /// <returns>
    /// <see langword="true"/> if this <see cref="AtUri"/> and the specified <paramref name="obj"/> refer to the same object,
    /// or <paramref name="obj"/> is an <see cref="AtUri"/>, or a type derived from it, with the same value; otherwise, <see langword="false"/>.
    /// </returns>
    public sealed override bool Equals([NotNullWhen(true)] object? obj) => Equals(obj as AtUri);

    /// <summary>
    /// Indicates where this <see cref="AtUri"/> equals another.
    /// </summary>
    /// <param name="other">A <see cref="AtUri"/> or <see langword="null"/> to compare to this <see cref="AtUri"/>.</param>
    /// <returns>
    /// <see langword="true"/> if this <see cref="AtUri"/> and the specified <paramref name="other"/> refer to the same object,
    /// or have the same value; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Equality is based on the value of the URI, not on its runtime type, so an <see cref="AtUri"/> created with the
    /// <see cref="AtUri(string)"/> constructor is equal to a <see cref="SpaceAtUri"/> with the same value.
    /// </para>
    /// </remarks>
    public bool Equals([NotNullWhen(true)] AtUri? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (IsSpaceUri != other.IsSpaceUri || !Authority.Equals(other.Authority))
        {
            return false;
        }

        if (!IsSpaceUri)
        {
            return string.Equals(Scheme, other.Scheme, StringComparison.Ordinal) &&
                   string.Equals(AbsolutePath, other.AbsolutePath, StringComparison.Ordinal);
        }

        return SpaceTypeCore == other.SpaceTypeCore &&
               string.Equals(SpaceKeyCore, other.SpaceKeyCore, StringComparison.Ordinal) &&
               AuthorDidCore == other.AuthorDidCore &&
               Collection == other.Collection &&
               RecordKey == other.RecordKey;
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
    public sealed override string ToString() => _value ??= Format();

    private string Format()
    {
        if (IsSpaceUri)
        {
            string space = $"{SpacePrefix}{Authority}/{SpaceTypeCore}/{SpaceKeyCore}";

            return AuthorDidCore is null ? space : $"{space}/{AuthorDidCore}/{Collection}/{RecordKey}";
        }

        string scheme = string.IsNullOrEmpty(Scheme) ? string.Empty : Scheme + "://";

        return string.Concat(scheme, Authority.ToString(), AbsolutePath);
    }

    /// <summary>
    /// Creates a <see cref="AtUri"/> from the specified string.
    /// </summary>
    /// <param name="s">The string to convert.</param>
    /// <returns>An <see cref="AtUri"/> from the specified string, which is a <see cref="SpaceAtUri"/> if <paramref name="s"/> is a space URI.</returns>
    /// <exception cref="ArgumentException"><paramref name="s"/> is <see langword="null"/>, empty or whitespace.</exception>
    /// <exception cref="AtUriFormatException"><paramref name="s"/> is not a valid AT URI.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator AtUri(string s) => Create(ParseOrThrow(s));

    /// <summary>
    /// Creates a <see cref="AtUri"/> from the specified string.
    /// </summary>
    /// <param name="s">The string to convert.</param>
    /// <returns>An <see cref="AtUri"/> from the specified string, which is a <see cref="SpaceAtUri"/> if <paramref name="s"/> is a space URI.</returns>
    /// <exception cref="ArgumentException"><paramref name="s"/> is <see langword="null"/>, empty or whitespace.</exception>
    /// <exception cref="AtUriFormatException"><paramref name="s"/> is not a valid AT URI.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AtUri FromString(string s) => s;

    /// <summary>
    /// Converts the string representation of an identifier to its <see cref="AtUri"/> equivalent.
    /// A return value indicates whether the operation succeeded.
    /// </summary>
    /// <param name="s">A string containing the id to convert.</param>
    /// <param name="result">
    /// When this method returns contains the <see cref="AtUri"/> equivalent of the
    /// string contained in s, which is a <see cref="SpaceAtUri"/> for a space URI, or <see langword="null"/> if the conversion failed. The conversion fails if the <paramref name="s"/> parameter
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

    /// <summary>
    /// Parses a string into an <see cref="AtUri"/>.
    /// </summary>
    /// <param name="s">The AT URI to parse.</param>
    /// <param name="provider">The format provider, which is ignored.</param>
    /// <returns>The parsed AT URI, which is a <see cref="SpaceAtUri"/> for a space URI.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="s"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException"><paramref name="s"/> is not a valid AT URI.</exception>
    public static AtUri Parse(string s, IFormatProvider? provider)
    {
        ArgumentNullException.ThrowIfNull(s);

        if (TryParse(s, out AtUri? result))
        {
            return result;
        }

        throw new FormatException("The value is not a valid AT URI.");
    }

    /// <summary>
    /// Tries to parse a string into an <see cref="AtUri"/>.
    /// </summary>
    /// <param name="s">The AT URI to parse.</param>
    /// <param name="provider">The format provider, which is ignored.</param>
    /// <param name="result">The parsed AT URI, which is a <see cref="SpaceAtUri"/> for a space URI, or <see langword="null"/> if parsing failed.</param>
    /// <returns><see langword="true"/> if the string is a valid AT URI; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out AtUri result)
    {
        if (s is null)
        {
            result = null;
            return false;
        }

        return TryParse(s, out result);
    }

    private static bool Parse(string s, bool throwOnError, out AtUri? result)
    {
        if (!Parse(s, throwOnError, out AtUriComponents components))
        {
            result = null;
            return false;
        }

        result = Create(components);
        return true;
    }

    /// <summary>
    /// Validates <paramref name="s"/> and parses it into its components, throwing if it is not a valid AT URI.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <returns>The components of the AT URI.</returns>
    /// <exception cref="ArgumentException"><paramref name="s"/> is <see langword="null"/>, empty or whitespace.</exception>
    /// <exception cref="AtUriFormatException"><paramref name="s"/> is not a valid AT URI.</exception>
    internal static AtUriComponents ParseOrThrow(string s)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(s);

        _ = Parse(s, true, out AtUriComponents components);
        return components;
    }

    /// <summary>
    /// Creates an <see cref="AtUri"/>, or a <see cref="SpaceAtUri"/> when the components describe a space URI.
    /// </summary>
    /// <param name="components">The validated components.</param>
    /// <returns>An <see cref="AtUri"/> whose runtime type matches the form of the URI.</returns>
#pragma warning disable BSKYSpaces
    private static AtUri Create(in AtUriComponents components) =>
        components.IsSpace ? new SpaceAtUri(components) : new AtUri(components);
#pragma warning restore BSKYSpaces

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
    internal static bool Parse(string s, bool throwOnError, out AtUriComponents components)
    {
        components = default;

        if (s.Length > MaximumLength)
        {
            return Fail(throwOnError, $"{s} is too long.");
        }

        bool isSpace = s.StartsWith(SpacePrefix, StringComparison.Ordinal);

        if (!isSpace && !s.StartsWith(ProtocolAndSeparator, StringComparison.Ordinal))
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

        if (isSpace)
        {
            return ParseSpace(s, throwOnError, out components);
        }

        AtIdentifier authority;
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
            components = new AtUriComponents(
                IsSpace: false,
                Authority: authority,
                AbsolutePath: null,
                SpaceType: null,
                SpaceKey: null,
                AuthorDid: null,
                Collection: null,
                RecordKey: null);
            return true;
        }

        string absolutePath = s[(ProtocolAndSeparator.Length + authorityLength)..];

        ReadOnlySpan<char> path = remaining[(authorityLength + 1)..];
        int collectionLength = path.IndexOf('/');
        ReadOnlySpan<char> collectionSegment = collectionLength < 0 ? path : path[..collectionLength];

        // Failures in either path segment are reported as an AtUriFormatException, so that every way an AT URI
        // can be malformed is reported the same way. The segment specific exceptions belong to callers parsing
        // a segment in isolation, not to callers parsing a URI.
        if (!Nsid.Parse(new string(collectionSegment), false, out Nsid? collection))
        {
            return Fail(throwOnError, $"Collection segment, {collectionSegment}, must be a valid NSID.");
        }

        if (collectionLength < 0)
        {
            components = new AtUriComponents(
                IsSpace: false,
                Authority: authority,
                AbsolutePath: absolutePath,
                SpaceType: null,
                SpaceKey: null,
                AuthorDid: null,
                Collection: collection,
                RecordKey: null);
            return true;
        }

        ReadOnlySpan<char> recordKeySegment = path[(collectionLength + 1)..];

        if (recordKeySegment.Contains('/'))
        {
            return Fail(throwOnError, $"{s} has too many segments");
        }

        if (!RecordKey.Parse(new string(recordKeySegment), false, out RecordKey? recordKey))
        {
            return Fail(throwOnError, $"Record key segment, {recordKeySegment}, must be a valid record key.");
        }

        components = new AtUriComponents(
            IsSpace: false,
            Authority: authority,
            AbsolutePath: absolutePath,
            SpaceType: null,
            SpaceKey: null,
            AuthorDid: null,
            Collection: collection,
            RecordKey: recordKey);
        return true;
    }

    // Space URI syntax comes from the permissioned data proposal,
    // https://github.com/bluesky-social/proposals/blob/119f2a0ab7639d00e47491c549e353c80b53f3c9/0016-permissioned-data/README.md#addressing
    // and the proposed reference implementation in @atproto/syntax, https://github.com/bluesky-social/atproto/pull/6010,
    //   SPACE-AT-URI = "at:space/" SPACE-DID "/" SPACE-TYPE "/" SKEY [ "/" AUTHOR-DID "/" COLLECTION "/" RKEY ]
    // Unlike a public AT URI both the space authority and the author must be DIDs, and only the space, or a record
    // in a space, may be addressed, so a space URI must have exactly three or six segments. A space key has the same
    // syntax as a record key. The query, fragment and character checks are shared with public AT URIs and have already
    // been made by the caller.
    private static bool ParseSpace(string s, bool throwOnError, out AtUriComponents components)
    {
        components = default;

        ReadOnlySpan<char> remaining = s.AsSpan(SpacePrefix.Length);

        // One more range than the longest valid form, so that any extra segments end up in the last range and are detected.
        Span<Range> segments = stackalloc Range[SpaceRecordSegmentCount + 1];
        int segmentCount = remaining.Split(segments, '/');

        if (segmentCount != SpaceSegmentCount && segmentCount != SpaceRecordSegmentCount)
        {
            return Fail(throwOnError, $"{s} must address either a space, or a record in a space.");
        }

        string spaceDidValue = new(remaining[segments[0]]);
        if (!spaceDidValue.StartsWith("did:", StringComparison.Ordinal) || !Did.TryParse(spaceDidValue, out Did? spaceDid))
        {
            return Fail(throwOnError, $"Space authority segment, {spaceDidValue}, must be a valid DID.");
        }

        string spaceTypeValue = new(remaining[segments[1]]);
        if (!Nsid.Parse(spaceTypeValue, false, out Nsid? spaceType))
        {
            return Fail(throwOnError, $"Space type segment, {spaceTypeValue}, must be a valid NSID.");
        }

        string spaceKey = new(remaining[segments[2]]);
        if (!RecordKey.Parse(spaceKey, false, out _))
        {
            return Fail(throwOnError, $"Space key segment, {spaceKey}, must be a valid space key.");
        }

        if (segmentCount == SpaceSegmentCount)
        {
            components = new AtUriComponents(
                IsSpace: true,
                Authority: spaceDid,
                AbsolutePath: null,
                SpaceType: spaceType,
                SpaceKey: spaceKey,
                AuthorDid: null,
                Collection: null,
                RecordKey: null);
            return true;
        }

        string authorDidValue = new(remaining[segments[3]]);
        if (!authorDidValue.StartsWith("did:", StringComparison.Ordinal) || !Did.TryParse(authorDidValue, out Did? authorDid))
        {
            return Fail(throwOnError, $"Author segment, {authorDidValue}, must be a valid DID.");
        }

        string collectionValue = new(remaining[segments[4]]);
        if (!Nsid.Parse(collectionValue, false, out Nsid? collection))
        {
            return Fail(throwOnError, $"Collection segment, {collectionValue}, must be a valid NSID.");
        }

        string recordKeyValue = new(remaining[segments[5]]);
        if (!RecordKey.Parse(recordKeyValue, false, out RecordKey? recordKey))
        {
            return Fail(throwOnError, $"Record key segment, {recordKeyValue}, must be a valid record key.");
        }

        components = new AtUriComponents(
            IsSpace: true,
            Authority: spaceDid,
            AbsolutePath: null,
            SpaceType: spaceType,
            SpaceKey: spaceKey,
            AuthorDid: authorDid,
            Collection: collection,
            RecordKey: recordKey);
        return true;
    }

    private static bool Fail(bool throwOnError, string message) =>
        throwOnError ? throw new AtUriFormatException(message) : false;

    /// <summary>
    /// Gets the value shown for this <see cref="AtUri"/> in the debugger.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A space URI held in a base <see cref="AtUri"/>, for example one created with the <see cref="AtUri(string)"/> constructor,
    /// is marked as a space URI so it can be told apart from a public AT URI without inspecting <see cref="IsSpaceUri"/>.
    /// </para>
    /// </remarks>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private string DebuggerDisplay => IsSpaceUri ? $"{this} (space URI)" : ToString();

    /// <summary>
    /// Holds the validated components of a public AT URI or a space URI.
    /// </summary>
    /// <param name="IsSpace">A flag indicating whether the components describe a space URI.</param>
    /// <param name="Authority">The authority of a public AT URI, or the space authority DID of a space URI.</param>
    /// <param name="AbsolutePath">The path of a public AT URI, which is always <see langword="null"/> for a space URI.</param>
    /// <param name="SpaceType">The space type of a space URI.</param>
    /// <param name="SpaceKey">The space key of a space URI.</param>
    /// <param name="AuthorDid">The author DID of a record in a space.</param>
    /// <param name="Collection">The collection of a record.</param>
    /// <param name="RecordKey">The record key of a record.</param>
    internal readonly record struct AtUriComponents(
        bool IsSpace,
        AtIdentifier Authority,
        string? AbsolutePath,
        Nsid? SpaceType,
        string? SpaceKey,
        Did? AuthorDid,
        Nsid? Collection,
        RecordKey? RecordKey);
}