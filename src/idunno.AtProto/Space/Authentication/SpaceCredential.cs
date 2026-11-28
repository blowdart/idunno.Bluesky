// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;

#pragma warning disable IDE0130 // Namespace does not match folder structure.
namespace idunno.AtProto.Space;
#pragma warning restore IDE0130 // Namespace does not match folder structure.

/// <summary>
/// Owns a verified, short-lived space credential and its private proof-of-possession key.
/// </summary>
/// <remarks>
/// <para>Dispose this object when access is no longer needed. Expiration automatically destroys the private key.
/// Obtain a new credential by authorizing again; credentials are not automatically renewed or persisted.
/// A credential is usable across the repository hosts of its one space, independently of the account session.</para>
/// </remarks>
[Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
public sealed class SpaceCredential : IDisposable
{
#if NET9_0_OR_GREATER
    private readonly Lock _gate = new();
#else
    private readonly object _gate = new();
#endif
    private readonly TimeProvider _clock;
    private ECDsa? _key;
    private string? _token;
    private ITimer? _expiryTimer;
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="SpaceCredential"/> class and takes ownership of its proof key.</summary>
    internal SpaceCredential(
        SpaceAtUri space,
        Did keyId,
        string token,
        ECDsa key,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt,
        string tokenId,
        TimestampIdentifier? spaceRev,
        TimeProvider clock)
    {
        Space = space;
        KeyId = keyId;
        IssuedAt = issuedAt;
        ExpiresAt = expiresAt;
        TokenId = tokenId;
        SpaceRev = spaceRev;
        _clock = clock;
        _key = key;
        _token = token;
        _expiryTimer = clock.CreateTimer(
            callback: _ => Expire(),
            state: null,
            dueTime: Timeout.InfiniteTimeSpan,
            period: Timeout.InfiniteTimeSpan);
        TimeSpan remaining = expiresAt - clock.GetUtcNow();
        _expiryTimer.Change(remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero, Timeout.InfiniteTimeSpan);
    }

    /// <summary>Gets the space to which this credential grants access.</summary>
    /// <value>The exact space reference verified during authorization.</value>
    public SpaceAtUri Space { get; }

    /// <summary>Gets the public P-256 key DID to which this credential is bound.</summary>
    /// <value>The public proof key's DID, not its private material.</value>
    public Did KeyId { get; }

    /// <summary>Gets the credential's verified issuance time.</summary>
    /// <value>The authority's signed issuance time.</value>
    public DateTimeOffset IssuedAt { get; }

    /// <summary>Gets the time at which the credential becomes unusable.</summary>
    /// <value>The authority's signed expiry time.</value>
    public DateTimeOffset ExpiresAt { get; }

    /// <summary>Gets the identifier used by the authority for revocation.</summary>
    /// <value>The verified JWT identifier.</value>
    public string TokenId { get; }

    /// <summary>Gets the space revision returned during authorization, if one exists.</summary>
    /// <value>The authority's revision, or <see langword="null"/> when none was returned.</value>
    public TimestampIdentifier? SpaceRev { get; }

    /// <summary>Gets a value that indicates whether the credential has expired.</summary>
    /// <value><see langword="true"/> if expiry has occurred; otherwise, <see langword="false"/>.</value>
    public bool IsExpired { get => Volatile.Read(ref field) || _clock.GetUtcNow() >= ExpiresAt; private set; }

    /// <summary>Destroys the private key and releases the credential and expiry timer.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            DestroyKey();
        }
    }

    /// <summary>Checks scope and lifetime before signing the exact credential and repository audience.</summary>
    internal void SignRequest(HttpRequestMessage request, SpaceAtUri space, Did audience)
    {
        lock (_gate)
        {
            ValidateUse(space);

            string authorization = "Atproto-Space-Credential " + _token;
            const string parameters = "(\"authorization\" \"atproto-space-audience\");tag=\"atproto-space-credential\"";
            string signatureBase = $"\"authorization\": {authorization}\n\"atproto-space-audience\": {audience}\n\"@signature-params\": {parameters}";
            byte[] signature = _key.SignData(
                Encoding.ASCII.GetBytes(signatureBase),
                HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            request.Headers.Add("Authorization", authorization);
            request.Headers.Add("Atproto-Space-Audience", audience.Value);
            request.Headers.Add("Signature-Input", "sig=" + parameters);
            request.Headers.Add("Signature", "sig=:" + Convert.ToBase64String(signature) + ":");
        }
    }

    /// <summary>Rejects unusable credentials before initiating identity resolution or an HTTP operation.</summary>
    internal void EnsureUsable(SpaceAtUri space)
    {
        lock (_gate)
        {
            ValidateUse(space);
        }
    }

    /// <summary>Enforces disposal, expiration and exact scope while holding the lifecycle lock.</summary>
    [MemberNotNull(nameof(_key), nameof(_token))]
    private void ValidateUse(SpaceAtUri space)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsExpired || _key is null || _token is null)
        {
            IsExpired = true;
            DestroyKey();
            throw new InvalidOperationException("The space credential has expired. Authorize the space again.");
        }

        if (space != Space)
        {
            throw new ArgumentException("The credential belongs to another space.", nameof(space));
        }
    }

    /// <summary>Erases an expired proof key without making expiration look like explicit disposal.</summary>
    private void Expire()
    {
        lock (_gate)
        {
            IsExpired = true;
            DestroyKey();
        }
    }

    /// <summary>Releases all owned secret material while holding the lifecycle lock.</summary>
    private void DestroyKey()
    {
        _key?.Dispose();
        _key = null;
        _token = null;
        _expiryTimer?.Dispose();
        _expiryTimer = null;
    }
}
