// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json.Serialization;

using idunno.AtProto.Repo;

namespace idunno.AtProto.Labels;

/// <summary>
/// Metadata tag on an atproto resource (eg, repo or record).
/// </summary>
/// <remarks>
/// <para>See https://github.com/bluesky-social/atproto/blob/main/lexicons/com/atproto/label/defs.json</para>
/// <para>See https://atproto.blue/en/latest/atproto/atproto_client.models.com.atproto.label.defs.html#atproto_client.models.com.atproto.label.defs.Label</para>
/// </remarks>
[SuppressMessage("Usage", "CA1054:Uri parameters should not be strings", Justification = "The label uri can be either an at uri or a did.")]
[SuppressMessage("Usage", "CA1056:Uri properties should not be strings", Justification = "The label uri can be either an at uri or a did.")]
[DebuggerDisplay("{DebuggerDisplay,nq}")]
[JsonConverter(typeof(LabelConverter))]
public sealed record Label : AtProtoObject
{
    private DateTimeOffset _creationTimestamp;
    private DateTimeOffset? _expiresAt;
    private Cid? _cid;
    private string? _cidText;
    private string? _creationTimestampText;
    private string? _expiresAtText;

    /// <summary>
    /// Creates a new instance of <see cref="Label"/>.
    /// </summary>
    /// <param name="version">The AT Protocol version of the label.</param>
    /// <param name="source"><see cref="Did"/> of the actor who created the <see cref="Label"/>.</param>
    /// <param name="uri"><see cref="AtUri"/> of the record, repository (account), or other resource that the <see cref="Label"/> applies to.</param>
    /// <param name="cid">Optional <see cref="Cid">Content Identifier</see> specifying the specific version of <paramref name="uri"/> resource the <see cref="Label"/> applies to.</param>
    /// <param name="value">The short string name of the value or type of the <see cref="Label"/>.</param>
    /// <param name="isNegationLabel">Flag indicating whether the <see cref="Label"/> is a negation label, overwriting a previous label.</param>
    /// <param name="creationTimestamp">Timestamp when the label was created.</param>
    /// <param name="signature">Signature of dag-cbor encoded label.</param>
    /// <exception cref="ArgumentNullException">Thrown when any of <paramref name="source"/>, <paramref name="uri"/> or <paramref name="value"/> are <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="value"/> is longer than 128 UTF-8 bytes.</exception>
    public Label(
        int? version,
        Did source,
        string uri,
        Cid? cid,
        string value,
        bool isNegationLabel,
        DateTimeOffset creationTimestamp,
        Bytes? signature)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value.GetUtf8Length(), 128);

        Version = version;
        Source = source;
        Uri = uri;
        Cid = cid;
        Value = value;
        IsNegationLabel = isNegationLabel;
        CreationTimestamp = creationTimestamp;

        Signature = signature;
    }

    /// <summary>
    /// The AT Protocol version of this <see cref="Label"/>.
    /// </summary>
    [JsonPropertyName("ver")]
    public int? Version { get; init; }

    /// <summary>
    /// <see cref="Did"/> of the actor who created this <see cref="Label"/>.
    /// </summary>
    [JsonRequired]
    [JsonPropertyName("src")]
    public Did Source { get; init; }

    /// <summary>
    /// A string which can either be a <see cref="Did"/>, to refer to an account level label, or an <see cref="AtUri"/> to reference to a record level label.
    /// </summary>
    /// <remarks>
    /// <para>Labels are a special case where uri can be a did to refer to an account level label, or an at uri to refer to a record</para>
    /// </remarks>
    [JsonRequired]
    public string Uri { get; init; }

    /// <summary>
    /// Optional <see cref="Cid">Content Identifier</see> specifying the specific version of <see cref="Uri"/> resource this <see cref="Label"/> applies to,
    /// if the Uri is an AT URI.
    /// </summary>
    public Cid? Cid
    {
        get => _cid;
        init
        {
            _cid = value;
            _cidText = null;
        }
    }

    /// <summary>
    /// The short string name of the value or type of this <see cref="Label"/>.
    /// </summary>
    [JsonRequired]
    [JsonPropertyName("val")]
    public string Value { get; init; }

    /// <summary>
    /// Flag indicating whether this <see cref="Label"/> is a negation label, overwriting a previous label
    /// </summary>
    [JsonPropertyName("neg")]
    public bool IsNegationLabel
    {
        get => Negation ?? false;
        init => Negation = value;
    }

    /// <summary>
    /// Timestamp when this label was created.
    /// </summary>
    [JsonRequired]
    [JsonPropertyName("cts")]
    public DateTimeOffset CreationTimestamp
    {
        get => _creationTimestamp;
        init
        {
            _creationTimestamp = value;
            _creationTimestampText = null;
        }
    }

    /// <summary>
    /// Gets the timestamp at which this label expires and no longer applies, if any.
    /// </summary>
    /// <value>The expiry time of the label, or <see langword="null"/> if the label does not expire.</value>
    [JsonPropertyName("exp")]
    public DateTimeOffset? ExpiresAt
    {
        get => _expiresAt;
        init
        {
            _expiresAt = value;
            _expiresAtText = null;
        }
    }

    // String representations and optional negation presence are part of the signed data.
    internal string? CidText
    {
        get => _cidText ?? _cid?.ToString();
        set
        {
            _cid = value is null ? null : new Cid(value);
            _cidText = value;
        }
    }

    internal string CreationTimestampText
    {
        get => _creationTimestampText ?? _creationTimestamp.ToString("O", CultureInfo.InvariantCulture);
        set
        {
            _creationTimestamp = ParseTimestamp(value);
            _creationTimestampText = value;
        }
    }

    internal string? ExpiresAtText
    {
        get => _expiresAtText ?? _expiresAt?.ToString("O", CultureInfo.InvariantCulture);
        set
        {
            _expiresAt = value is null ? null : ParseTimestamp(value);
            _expiresAtText = value;
        }
    }

    internal bool? Negation { get; set; }

    /// <summary>
    /// Signature of dag-cbor encoded label, if the label was signed.
    /// </summary>
    /// <remarks>
    /// <para>The <c>sig</c> property is optional in <see href="https://github.com/bluesky-social/atproto/blob/main/lexicons/com/atproto/label/defs.json">com.atproto.label.defs</see>,
    /// so this will be <see langword="null" /> for labels which carry no signature.</para>
    /// <para>Over JSON a signature is encoded as a <c>$bytes</c> object, as the AT Protocol data model specifies, and over
    /// DAG-CBOR it is raw bytes. <see cref="Bytes"/> represents both.</para>
    /// </remarks>
    [JsonPropertyName("sig")]
    public Bytes? Signature { get; init; }

    /// <summary>
    /// Gets the canonical unsigned payload used for signing this label.
    /// </summary>
    /// <returns>A new array containing the version-one signed fields encoded as canonical DAG-CBOR.</returns>
    /// <exception cref="InvalidDataException">The version is missing or unsupported, or the signed fields are invalid.</exception>
    /// <remarks>
    /// <para>A signature is not required. Exact retained timestamp and CID strings and optional field presence are preserved.
    /// The signature, <c>$type</c>, and unknown fields are excluded.</para>
    /// </remarks>
    public byte[] GetCanonicalUnsignedBytes() => LabelEncoding.Encode(this);

    /// <summary>
    /// Verifies this label's signature against the issuer's DID document without making network requests.
    /// </summary>
    /// <param name="didDocument">The issuer's DID document obtained from a trusted identity resolver.</param>
    /// <exception cref="ArgumentNullException"><paramref name="didDocument"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidDataException">The label is unsigned, has an unsupported version or an invalid signature, or the document has no matching label key.</exception>
    /// <exception cref="System.Security.Cryptography.CryptographicException">The platform cannot use the key's curve.</exception>
    /// <remarks>
    /// <para>Verification does not establish trust in the issuer or evaluate expiry, negation, or supersession.</para>
    /// </remarks>
    public void Verify(DidDocument didDocument)
    {
        ArgumentNullException.ThrowIfNull(didDocument);
        LabelSignatureVerifier.VerifyLabel(this, SigningKeyVerifier.GetKey(didDocument, Source, SigningKeyVerifier.LabelSigningKeyFragment));
    }

    /// <summary>
    /// Resolves the issuer's DID document and verifies this label's signature.
    /// </summary>
    /// <param name="httpClient">An optional HTTP client used for identity resolution.</param>
    /// <param name="cancellationToken">A cancellation token for the operation.</param>
    /// <returns>A task which completes when the signature has been verified.</returns>
    /// <exception cref="InvalidDataException">The issuer cannot be resolved, or the label cannot be verified.</exception>
    /// <exception cref="System.Security.Cryptography.CryptographicException">The platform cannot use the key's curve.</exception>
    /// <exception cref="OperationCanceledException">The operation was cancelled.</exception>
    /// <remarks>
    /// <para>This method makes network requests and does not cache the DID document or retry verification with a refreshed key.
    /// For multiple labels from one issuer, resolve the document once and use <see cref="LabelSignatureVerifier.Verify"/>.</para>
    /// <para>Verification does not establish trust in the issuer or evaluate expiry, negation, or supersession.</para>
    /// </remarks>
    public async Task VerifyAsync(HttpClient? httpClient = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DidDocument? document = await IdentityResolution.ResolveDidDocumentAsync(
            Source, httpClient: httpClient, cancellationToken: cancellationToken).ConfigureAwait(false);
        Verify(document ?? throw new InvalidDataException($"The DID document for '{Source}' could not be resolved."));
    }

    /// <summary>
    /// Determines whether another label has the same property values.
    /// </summary>
    /// <param name="other">The label to compare with this label.</param>
    /// <returns><see langword="true"/> if the labels have equal property values; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// <para>Equality compares public property values, not their original wire representations.
    /// Equal labels can therefore have different signed payloads.</para>
    /// </remarks>
    public bool Equals(Label? other) =>
        other is not null &&
        Version == other.Version &&
        Source == other.Source &&
        Uri == other.Uri &&
        Cid == other.Cid &&
        Value == other.Value &&
        IsNegationLabel == other.IsNegationLabel &&
        CreationTimestamp == other.CreationTimestamp &&
        ExpiresAt == other.ExpiresAt &&
        Equals(Signature, other.Signature);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        HashCode hash = new();
        hash.Add(Version);
        hash.Add(Source);
        hash.Add(Uri);
        hash.Add(Cid);
        hash.Add(Value);
        hash.Add(IsNegationLabel);
        hash.Add(CreationTimestamp);
        hash.Add(ExpiresAt);
        hash.Add(Signature);
        return hash.ToHashCode();
    }

    private static DateTimeOffset ParseTimestamp(string value)
    {
        return ProtocolTimestamp.TryParse(value, out DateTimeOffset result)
            ? result
            : throw new InvalidDataException("The label timestamp is not a valid AT Protocol datetime.");
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private string DebuggerDisplay => '{' + Value + '}';
}
