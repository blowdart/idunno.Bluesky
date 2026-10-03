// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Authentication;

/// <summary>
/// Configuration options for OAuth authentication.
/// </summary>
public sealed class OAuthOptions
{

    /// <summary>
    /// Configuration Provider Key
    /// </summary>
    public const string AtProto = "AtProto";

    /// <summary>
    /// Configuration Provider Key
    /// </summary>
    public const string AtProtoOAuth = "OAuth";

    /// <summary>
    /// The default clock skew allowed when validating the lifetime of an issued token.
    /// </summary>
    public static readonly TimeSpan DefaultClockSkew = TimeSpan.FromMinutes(5);

    /// <summary>
    /// The default amount by which client assertion issued-at timestamps are backdated.
    /// </summary>
    public static readonly TimeSpan DefaultClientAssertionClockSkew = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Creates a new instance of <see cref="OAuthOptions"/>.
    /// </summary>
    public OAuthOptions()
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="OAuthOptions"/>.
    /// </summary>
    /// <param name="clientId">The OAuth client id.</param>
    /// <param name="returnUri">The return <see cref="Uri"/> an oauth authentication flow should return to.</param>
    /// <param name="scopes">The list of permissions to request.</param>
    public OAuthOptions(string clientId, Uri? returnUri = null, IEnumerable<string>? scopes = null)
    {
        ClientId = clientId;

        ReturnUri = returnUri;

        if (scopes is not null)
        {
            Scopes = scopes;
        }
    }

    /// <summary>
    /// Check that the options are valid.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Thrown when <see cref="ClientId"/> is white space, when a client signing key or key path is set and
    /// <see cref="ClientId"/> is a localhost development client ID, when additional client signing keys or key paths are set
    /// without an active client signing key or key path, or when two client signing keys share a key identifier.
    /// </exception>
    /// <exception cref="ArgumentNullException">Thrown when <see cref="ClientId"/> or <see cref="Scopes"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <see cref="Scopes"/> is empty.</exception>
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ClientId);
        ArgumentNullException.ThrowIfNull(Scopes);
        ArgumentOutOfRangeException.ThrowIfZero(Scopes.Count());

        bool hasActiveKey = ClientSigningKey is not null || !string.IsNullOrWhiteSpace(ClientSigningKeyPath);
        bool hasAdditionalKeys =
            AdditionalClientSigningKeys.Count != 0 ||
            AdditionalClientSigningKeyPaths.Any(path => !string.IsNullOrWhiteSpace(path));

        if ((hasActiveKey || hasAdditionalKeys) &&
            Uri.TryCreate(ClientId, UriKind.Absolute, out Uri? clientId) &&
            clientId.Scheme == Uri.UriSchemeHttp &&
            string.Equals(clientId.Host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Localhost development client IDs are public clients and cannot use a client signing key.");
        }

        if (hasAdditionalKeys && !hasActiveKey)
        {
            throw new ArgumentException("Additional client signing keys require an active client signing key.");
        }

        if (GetClientSigningKeys().GroupBy(key => key.KeyId, StringComparer.Ordinal).Any(group => group.Count() > 1))
        {
            throw new ArgumentException("Each client signing key must have a unique key identifier.");
        }
    }

    /// <summary>
    /// Gets the active client signing key followed by any additional client signing keys.
    /// </summary>
    /// <returns>The configured client signing keys.</returns>
    internal IEnumerable<OAuthClientSigningKey> GetClientSigningKeys()
    {
        if (ClientSigningKey is not null)
        {
            yield return ClientSigningKey;
        }

        foreach (OAuthClientSigningKey key in AdditionalClientSigningKeys.Where(key => key is not null))
        {
            yield return key;
        }
    }

    /// <summary>
    /// Gets the client signing key a session authenticates with.
    /// </summary>
    /// <param name="keyId">The identifier of the key the session started with, or <see langword="null"/> if it is not known.</param>
    /// <param name="found">
    /// <see langword="true"/> when <paramref name="keyId"/> is <see langword="null"/> or matches a configured key; otherwise <see langword="false"/>.
    /// </param>
    /// <returns>
    /// The key matching <paramref name="keyId"/>, or <see cref="ClientSigningKey"/> when <paramref name="keyId"/> is
    /// <see langword="null"/> or does not match a configured key.
    /// </returns>
    internal OAuthClientSigningKey? GetClientSigningKey(string? keyId, out bool found)
    {
        found = true;

        if (keyId is null)
        {
            return ClientSigningKey;
        }

        OAuthClientSigningKey? match = GetClientSigningKeys().FirstOrDefault(key => string.Equals(key.KeyId, keyId, StringComparison.Ordinal));
        if (match is not null)
        {
            return match;
        }

        found = false;
        return ClientSigningKey;
    }

    /// <summary>
    /// Gets or sets the OAuth client id.
    /// </summary>
    public string ClientId { get; set; } = default!;

    /// <summary>
    /// Gets or sets the optional human-readable client name advertised in OAuth client metadata.
    /// </summary>
    public string? ClientName { get; set; }

    /// <summary>
    /// Gets or sets the optional client homepage URL advertised in OAuth client metadata.
    /// </summary>
    /// <remarks>
    /// <para>The homepage must have the same hostname as <see cref="ClientId"/> when publishing AT Protocol OAuth client metadata.</para>
    /// </remarks>
    public Uri? ClientUri { get; set; }

    /// <summary>
    /// Gets or sets the optional HTTPS terms of service URL advertised in OAuth client metadata.
    /// </summary>
    public Uri? TosUri { get; set; }

    /// <summary>
    /// Gets or sets the optional HTTPS privacy policy URL advertised in OAuth client metadata.
    /// </summary>
    public Uri? PolicyUri { get; set; }

    /// <summary>
    /// Gets or sets the key a confidential client authenticates itself to authorization servers with.
    /// </summary>
    /// <value>The client signing key, or <see langword="null"/> for a public client. The default is <see langword="null"/>.</value>
    /// <remarks>
    /// <para>
    ///   When set, pushed authorization, token, refresh and revocation requests include a <c>private_key_jwt</c> client
    ///   assertion signed with this key, and the client metadata must publish its public key. Localhost development client IDs
    ///   are always public clients and cannot use a signing key.
    /// </para>
    /// <para>
    ///   Sessions are bound to the key that started them. Changing or removing the key ends existing sessions when they next
    ///   refresh, unless the previous key is kept in <see cref="AdditionalClientSigningKeys"/>.
    /// </para>
    /// <para>
    ///   This property is not bound from configuration. Create it with <see cref="OAuthClientSigningKey.FromPem(string, string?)"/>
    ///   or <see cref="OAuthClientSigningKey.FromPemFile(string, string?)"/>, or, in ASP.NET Core, set <see cref="ClientSigningKeyPath"/>.
    /// </para>
    /// </remarks>
    public OAuthClientSigningKey? ClientSigningKey { get; set; }

    /// <summary>
    /// Gets or sets the path of a file containing the confidential client signing key.
    /// </summary>
    /// <value>
    /// The path of a file containing an unencrypted ECDSA P-256 private key in PKCS#8 or SEC 1 PEM format,
    /// or <see langword="null"/>. The default is <see langword="null"/>.
    /// </value>
    /// <remarks>
    /// <para>
    ///   This can be bound from configuration. The ASP.NET Core authentication package loads the key from this path into
    ///   <see cref="ClientSigningKey"/> when the options are built, unless <see cref="ClientSigningKey"/> is already set.
    ///   Environment variables in the path are expanded, a leading <c>~</c> is replaced with the user's profile directory,
    ///   and a relative path is resolved against the application's content root.
    /// </para>
    /// <para>
    ///   Other applications should load the key with <see cref="OAuthClientSigningKey.FromPemFile(string, string?)"/> and set
    ///   <see cref="ClientSigningKey"/> themselves.
    /// </para>
    /// </remarks>
    public string? ClientSigningKeyPath { get; set; }

    /// <summary>
    /// Gets or sets the key identifier to publish for the key loaded from <see cref="ClientSigningKeyPath"/>.
    /// </summary>
    /// <value>
    /// The key identifier, or <see langword="null"/> to use the RFC 7638 thumbprint of the public key. The default is <see langword="null"/>.
    /// </value>
    public string? ClientSigningKeyId { get; set; }

    /// <summary>
    /// Gets or sets the amount by which client assertion issued-at timestamps are backdated.
    /// </summary>
    /// <value>The clock skew allowance. The default is 30 seconds.</value>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <remarks>
    /// <para>
    ///   This accommodates small clock differences with authorization servers when authenticating confidential clients.
    ///   It applies to pushed authorization, token, refresh and revocation requests, without changing the assertion's
    ///   expiration one minute after creation. Set to <see cref="TimeSpan.Zero"/> to disable backdating.
    /// </para>
    /// <para>
    ///   This can be bound from configuration and is separate from <see cref="ClockSkew"/>, which controls token validation.
    ///   Keep the system clock synchronized; an authorization server may reject assertions backdated too far.
    /// </para>
    /// </remarks>
    public TimeSpan ClientAssertionClockSkew
    {
        get;

        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, TimeSpan.Zero);

            field = value;
        }
    } = DefaultClientAssertionClockSkew;

    /// <summary>
    /// Gets the keys, other than <see cref="ClientSigningKey"/>, that existing sessions may still authenticate with.
    /// </summary>
    /// <value>The additional client signing keys. The default is an empty collection.</value>
    /// <remarks>
    /// <para>
    ///   Use additional keys to rotate the client signing key without ending existing sessions. New sign-ins always use
    ///   <see cref="ClientSigningKey"/>. A session that started with an additional key keeps using that key when it refreshes
    ///   or signs out, provided the session's <see cref="DPoPAccessCredentials.ClientSigningKeyId"/> was saved with it.
    ///   The client metadata must publish every key.
    /// </para>
    /// <para>
    ///   Each key must have a unique key identifier, and additional keys require an active <see cref="ClientSigningKey"/>.
    /// </para>
    /// <para>
    ///   This property is not bound from configuration. In ASP.NET Core, set <see cref="AdditionalClientSigningKeyPaths"/> instead.
    /// </para>
    /// </remarks>
    public ICollection<OAuthClientSigningKey> AdditionalClientSigningKeys { get; } = [];

    /// <summary>
    /// Gets the paths of files containing additional client signing keys.
    /// </summary>
    /// <value>
    /// The paths of files containing unencrypted ECDSA P-256 private keys in PKCS#8 or SEC 1 PEM format. The default is an empty collection.
    /// </value>
    /// <remarks>
    /// <para>
    ///   This can be bound from configuration. The ASP.NET Core authentication package loads each key into
    ///   <see cref="AdditionalClientSigningKeys"/> when the options are built, resolving each path in the same way as
    ///   <see cref="ClientSigningKeyPath"/>. Each key's identifier is the RFC 7638 thumbprint of its public key.
    /// </para>
    /// </remarks>
    public ICollection<string> AdditionalClientSigningKeyPaths { get; } = [];

    /// <summary>
    /// Gets or sets the <see cref="Uri"/> the OAuth server should call back to when it has authenticated the user.
    /// </summary>
    public Uri? ReturnUri { get; set; } = default!;

    /// <summary>
    /// Gets or sets the clock skew allowed when validating the lifetime of an issued token.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when setting to a negative <see cref="TimeSpan"/>.</exception>
    /// <remarks>
    /// <para>
    /// Defaults to <see cref="DefaultClockSkew"/>. Set to <see cref="TimeSpan.Zero"/> to require that the
    /// clocks of the local machine and the authorization server agree exactly.
    /// </para>
    /// </remarks>
    public TimeSpan ClockSkew
    {
        get;

        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, TimeSpan.Zero);

            field = value;
        }
    } = DefaultClockSkew;

    /// <summary>
    /// Gets or sets a flag indicating whether the agent may use HTTP rather than HTTPS when talking to an
    /// authorization server or a personal data server. Defaults to <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    ///   This applies both to the validation of a discovered endpoint and to the transport the request is then made over,
    ///   so a single setting is enough to reach a server which does not serve HTTPS.
    /// </para>
    /// <para>
    ///   HTTP is also allowed on the transport, without this being set, when <see cref="ReturnUri"/> itself uses HTTP,
    ///   because an application whose own callback is not served over HTTPS is by definition not in a position to require it.
    /// </para>
    /// <para>
    ///   Only turn this on for local development, as in the localhost client setup described at
    ///   <see href="https://atproto.com/specs/oauth#clients">atproto.com</see>. Access and refresh tokens, and the
    ///   authorization code they are exchanged for, all travel over this connection.
    /// </para>
    /// </remarks>
    public bool AllowInsecureProtocols { get; set; }

    /// <summary>
    /// Gets or sets a flag indicating whether the agent may talk to an authorization server or a personal data server
    /// on a loopback address. Defaults to <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    ///   This applies both to the validation of a discovered endpoint and to the transport the request is then made over,
    ///   so a single setting is enough to reach a server running on the local machine.
    /// </para>
    /// <para>
    ///   Loopback addresses are also allowed on the transport, without this being set, when <see cref="ReturnUri"/> is
    ///   itself a loopback address.
    /// </para>
    /// <para>
    ///   Only turn this on for local development. Allowing loopback removes a protection against a hostile handle
    ///   resolving to a service which is only reachable from the machine the agent is running on.
    /// </para>
    /// </remarks>
    public bool AllowLoopback { get; set; }

    /// <summary>
    /// Gets a value indicating whether HTTP may be used on the wire, taking both <see cref="AllowInsecureProtocols"/>
    /// and the scheme of <see cref="ReturnUri"/> into account.
    /// </summary>
    internal bool AllowInsecureProtocolsOnTheWire =>
        AllowInsecureProtocols || (ReturnUri is not null && ReturnUri.Scheme == Uri.UriSchemeHttp);

    /// <summary>
    /// Gets a value indicating whether a loopback address may be used on the wire, taking both <see cref="AllowLoopback"/>
    /// and <see cref="ReturnUri"/> into account.
    /// </summary>
    internal bool AllowLoopbackOnTheWire =>
        AllowLoopback || (ReturnUri is not null && ReturnUri.IsLoopback);

    /// <summary>
    /// Gets or sets the list of permissions to request.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when setting to <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when setting to an empty collection.</exception>
    /// <remarks>
    /// <para>
    ///   The value supplied is copied, so later changes to the collection assigned are not reflected here, and reading
    ///   this does not re-enumerate the caller's collection.
    /// </para>
    /// </remarks>
    public IEnumerable<string> Scopes
    {
        get;

        set
        {
            ArgumentNullException.ThrowIfNull(value);

            string[] scopes = [.. value.Distinct(StringComparer.Ordinal)];

            ArgumentOutOfRangeException.ThrowIfZero(scopes.Length);

            field = scopes;
        }
    } = ["atproto"];

    /// <summary>
    /// Gets or sets the published permission sets to request in addition to <see cref="Scopes"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException">The collection or one of its entries is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>The collection is copied when assigned. The default is an empty collection.</para>
    /// <para>Explicit scopes passed to an OAuth request override both configured scopes and permission sets.</para>
    /// <para>Configuration binding accepts objects with a string <c>Nsid</c> and an optional string <c>Audience</c>.
    /// Enable <c>ErrorOnUnknownConfiguration</c> when binding to reject invalid collection entries rather than skipping them.</para>
    /// </remarks>
    public IEnumerable<OAuthPermissionSet> PermissionSets
    {
        get;

        set
        {
            ArgumentNullException.ThrowIfNull(value);

            OAuthPermissionSet[] permissionSets = [.. value];
            foreach (OAuthPermissionSet permissionSet in permissionSets)
            {
                ArgumentNullException.ThrowIfNull(permissionSet);
            }

            field = Array.AsReadOnly(permissionSets);
        }
    } = [];

    /// <summary>
    /// Gets the configured scopes combined with the permission-set references.
    /// </summary>
    /// <returns>A snapshot of the scopes to request, deduplicated using ordinal comparison.</returns>
    public IEnumerable<string> GetRequestedScopes() =>
        Scopes.Concat(PermissionSets.Select(permissionSet => permissionSet.ToString())).Distinct(StringComparer.Ordinal).ToArray();
}