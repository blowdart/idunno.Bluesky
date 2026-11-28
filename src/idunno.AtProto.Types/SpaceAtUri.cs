// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace idunno.AtProto;

/// <summary>
/// Provides an object representation of an AT URI that addresses permissioned space data, either a space or a record in a space.
/// </summary>
/// <remarks>
/// <para>A space URI addressing a space has the form <c>at:space/{spaceDid}/{spaceType}/{skey}</c>.</para>
/// <para>A space URI addressing a record in a space has the form <c>at:space/{spaceDid}/{spaceType}/{skey}/{authorDid}/{collection}/{rkey}</c>.</para>
/// <para>
/// <see cref="AtUri.Authority"/> is the space authority DID, <see cref="AtUri.Repo"/> is always <see langword="null"/>,
/// and <see cref="AtUri.Collection"/> and <see cref="AtUri.RecordKey"/> address the record within the space.
/// </para>
/// <para>
/// A <see cref="SpaceAtUri"/> is equal to an <see cref="AtUri"/> with the same value, regardless of how either was created.
/// </para>
/// <para>See https://github.com/bluesky-social/proposals/tree/main/0016-permissioned-data for the space URI proposal.</para>
/// </remarks>
[JsonConverter(typeof(Json.SpaceAtUriConverter))]
[DebuggerDisplay("{DebuggerDisplay,nq}")]
[Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
public sealed class SpaceAtUri : AtUri, IParsable<SpaceAtUri>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SpaceAtUri"/> class from validated components.
    /// </summary>
    /// <param name="components">The validated components of a space URI.</param>
    internal SpaceAtUri(in AtUriComponents components) : base(components)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SpaceAtUri"/> class from the specified string.
    /// </summary>
    /// <param name="s">The space URI.</param>
    /// <exception cref="ArgumentException"><paramref name="s"/> is <see langword="null"/>, empty or whitespace.</exception>
    /// <exception cref="AtUriFormatException"><paramref name="s"/> is not a valid AT URI, or is a public AT URI rather than a space URI.</exception>
    public SpaceAtUri(string s) : this(ParseSpaceOrThrow(s))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SpaceAtUri"/> class that addresses a space.
    /// </summary>
    /// <param name="spaceDid">The DID of the space authority.</param>
    /// <param name="spaceType">The NSID of the space type.</param>
    /// <param name="spaceKey">The space key.</param>
    /// <exception cref="ArgumentNullException"><paramref name="spaceDid"/>, <paramref name="spaceType"/> or <paramref name="spaceKey"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="spaceKey"/> is not a valid space key.</exception>
    public SpaceAtUri(Did spaceDid, Nsid spaceType, string spaceKey) : this(CreateComponents(spaceDid, spaceType, spaceKey))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SpaceAtUri"/> class that addresses a record in a space.
    /// </summary>
    /// <param name="spaceDid">The DID of the space authority.</param>
    /// <param name="spaceType">The NSID of the space type.</param>
    /// <param name="spaceKey">The space key.</param>
    /// <param name="authorDid">The DID of the author of the record.</param>
    /// <param name="collection">The collection of the record.</param>
    /// <param name="recordKey">The record key of the record.</param>
    /// <exception cref="ArgumentNullException">Any parameter is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="spaceKey"/> is not a valid space key.</exception>
    public SpaceAtUri(
        Did spaceDid,
        Nsid spaceType,
        string spaceKey,
        Did authorDid,
        Nsid collection,
        RecordKey recordKey) : this(CreateComponents(
            spaceDid: spaceDid,
            spaceType: spaceType,
            spaceKey: spaceKey,
            authorDid: authorDid,
            collection: collection,
            recordKey: recordKey))
    {
    }

    /// <summary>
    /// Gets the DID of the space authority.
    /// </summary>
    /// <value>The DID of the space authority, which is also returned by <see cref="AtUri.Authority"/>.</value>
    [JsonIgnore]
    public Did SpaceDid => (Did)Authority;

    /// <summary>
    /// Gets the NSID of the space type.
    /// </summary>
    /// <value>The NSID which resolves to the space type declaration.</value>
    [JsonIgnore]
    public Nsid SpaceType => SpaceTypeCore!;

    /// <summary>
    /// Gets the space key, which distinguishes spaces of the same type under the same space authority.
    /// </summary>
    /// <value>The space key.</value>
    /// <remarks>
    /// <para>A space key has the same syntax as a record key, for example a slug, a TID, or a reserved string such as <c>self</c>.</para>
    /// </remarks>
    [JsonIgnore]
    public string SpaceKey => SpaceKeyCore!;

    /// <summary>
    /// Gets the DID of the author of the record addressed by this space URI.
    /// </summary>
    /// <value>The DID of the record author, or <see langword="null"/> if this space URI addresses a space rather than a record.</value>
    [JsonIgnore]
    public Did? AuthorDid => AuthorDidCore;

    /// <summary>
    /// Gets a value that indicates whether this space URI addresses a record in a space.
    /// </summary>
    /// <value>
    /// <see langword="true"/> if this space URI addresses a record in a space; otherwise, <see langword="false"/> if it addresses a space.
    /// </value>
    /// <remarks>
    /// <para>When this property is <see langword="true"/>, <see cref="AuthorDid"/>, <see cref="AtUri.Collection"/> and <see cref="AtUri.RecordKey"/> are not <see langword="null"/>.</para>
    /// </remarks>
    [JsonIgnore]
    [MemberNotNullWhen(true, nameof(AuthorDid))]
    public bool IsRecord => AuthorDid is not null && Collection is not null && RecordKey is not null;

    /// <summary>
    /// Gets the value shown for this <see cref="SpaceAtUri"/> in the debugger, which indicates whether it addresses a space or a record in a space.
    /// </summary>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private string DebuggerDisplay => IsRecord ? $"{this} (space record)" : $"{this} (space)";

    /// <summary>
    /// Parses a string into a <see cref="SpaceAtUri"/>.
    /// </summary>
    /// <param name="s">The space URI to parse.</param>
    /// <param name="provider">The format provider, which is ignored.</param>
    /// <returns>The parsed space URI.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="s"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException"><paramref name="s"/> is not a valid space URI.</exception>
    public static new SpaceAtUri Parse(string s, IFormatProvider? provider)
    {
        ArgumentNullException.ThrowIfNull(s);

        if (TryParse(s, out SpaceAtUri? result))
        {
            return result;
        }

        throw new FormatException("The value is not a valid space URI.");
    }

    /// <summary>
    /// Tries to parse a string into a <see cref="SpaceAtUri"/>.
    /// </summary>
    /// <param name="s">The space URI to parse.</param>
    /// <param name="provider">The format provider, which is ignored.</param>
    /// <param name="result">
    /// When this method returns, contains the parsed space URI, or <see langword="null"/> if parsing failed. This parameter is treated as uninitialized.
    /// </param>
    /// <returns><see langword="true"/> if <paramref name="s"/> is a valid space URI; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out SpaceAtUri result) =>
        TryParse(s, out result);

    /// <summary>
    /// Tries to parse a string into a <see cref="SpaceAtUri"/>.
    /// </summary>
    /// <param name="s">The space URI to parse.</param>
    /// <param name="result">
    /// When this method returns, contains the parsed space URI, or <see langword="null"/> if parsing failed. This parameter is treated as uninitialized.
    /// </param>
    /// <returns><see langword="true"/> if <paramref name="s"/> is a valid space URI; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// <para>A valid public AT URI, which begins <c>at://</c>, is not a space URI, and parsing it returns <see langword="false"/>.</para>
    /// </remarks>
    public static bool TryParse([NotNullWhen(true)] string? s, [NotNullWhen(true)] out SpaceAtUri? result)
    {
        if (string.IsNullOrEmpty(s) || !Parse(s, false, out AtUriComponents components) || !components.IsSpace)
        {
            result = null;
            return false;
        }

        result = new SpaceAtUri(components);
        return true;
    }

    /// <summary>
    /// Tries to parse a space URI read from an alpha-era record value, accepting the legacy space URI form.
    /// </summary>
    /// <param name="s">The space URI read from a record value.</param>
    /// <param name="result">When this method returns, contains the normalized space URI, or <see langword="null"/> if parsing failed. This parameter is treated as uninitialized.</param>
    /// <returns><see langword="true"/> if <paramref name="s"/> is a valid current or legacy space URI; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// <para>The legacy form is <c>at://{spaceDid}/space/{spaceType}/{skey}</c>, optionally followed by
    /// <c>/{authorDid}/{collection}/{rkey}</c>. The returned URI always uses the current <c>at:space/</c> form.</para>
    /// <para>This temporary compatibility path is only for values read from alpha-era records. Do not use it to validate
    /// endpoint parameters, credentials, MAC headers, archives, or new record input. To validate a space reference,
    /// additionally check that <see cref="IsRecord"/> is <see langword="false"/>.</para>
    /// </remarks>
    public static bool TryParseRecordValue([NotNullWhen(true)] string? s, [NotNullWhen(true)] out SpaceAtUri? result)
    {
        if (TryParse(s, out result))
        {
            return true;
        }

        if (s is null || s.Length > MaximumLength || !s.StartsWith("at://", StringComparison.Ordinal))
        {
            return false;
        }

        int authorityEnd = s.IndexOf('/', 5);
        const string marker = "/space/";
        if (authorityEnd < 0 || !s.AsSpan(authorityEnd).StartsWith(marker, StringComparison.Ordinal))
        {
            return false;
        }

        string normalized = $"at:space/{s[5..authorityEnd]}/{s[(authorityEnd + marker.Length)..]}";
        return TryParse(normalized, out result);
    }

    /// <summary>
    /// Validates <paramref name="s"/> as a space URI and parses it into its components.
    /// </summary>
    /// <param name="s">The string to parse.</param>
    /// <returns>The components of the space URI.</returns>
    /// <exception cref="ArgumentException"><paramref name="s"/> is <see langword="null"/>, empty or whitespace.</exception>
    /// <exception cref="AtUriFormatException"><paramref name="s"/> is not a valid AT URI, or is not a space URI.</exception>
    private static AtUriComponents ParseSpaceOrThrow(string s)
    {
        AtUriComponents components = ParseOrThrow(s);

        if (!components.IsSpace)
        {
            throw new AtUriFormatException($"{s} is not a space URI.");
        }

        return components;
    }

    /// <summary>
    /// Validates the components of a space URI supplied as individual arguments.
    /// </summary>
    /// <param name="spaceDid">The DID of the space authority.</param>
    /// <param name="spaceType">The NSID of the space type.</param>
    /// <param name="spaceKey">The space key, which must have record key syntax.</param>
    /// <param name="authorDid">The DID of the record author, or <see langword="null"/> for a space.</param>
    /// <param name="collection">The collection of the record, or <see langword="null"/> for a space.</param>
    /// <param name="recordKey">The record key of the record, or <see langword="null"/> for a space.</param>
    /// <returns>The validated components.</returns>
    /// <remarks>
    /// <para>The public constructors guarantee that the record components are either all present or all absent.</para>
    /// </remarks>
    private static AtUriComponents CreateComponents(
        Did spaceDid,
        Nsid spaceType,
        string spaceKey,
        Did? authorDid = null,
        Nsid? collection = null,
        RecordKey? recordKey = null)
    {
        ArgumentNullException.ThrowIfNull(spaceDid);
        ArgumentNullException.ThrowIfNull(spaceType);
        ArgumentNullException.ThrowIfNull(spaceKey);

        if (!RecordKey.Parse(spaceKey, false, out _))
        {
            throw new ArgumentException($"{spaceKey} is not a valid space key.", nameof(spaceKey));
        }

        return new AtUriComponents(
            IsSpace: true,
            Authority: spaceDid,
            AbsolutePath: null,
            SpaceType: spaceType,
            SpaceKey: spaceKey,
            AuthorDid: authorDid,
            Collection: collection,
            RecordKey: recordKey);
    }
}
