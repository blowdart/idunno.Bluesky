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
    /// <exception cref="ArgumentException">Thrown when <see cref="ClientId"/> is white space.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <see cref="ClientId"/> or <see cref="Scopes"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <see cref="Scopes"/> is empty.</exception>
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ClientId);
        ArgumentNullException.ThrowIfNull(Scopes);
        ArgumentOutOfRangeException.ThrowIfZero(Scopes.Count());
    }

    /// <summary>
    /// Gets or sets the OAuth client id.
    /// </summary>
    public string ClientId { get; set; } = default!;

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
    public IEnumerable<string> Scopes
    {
        get;

        set
        {
            ArgumentNullException.ThrowIfNull(value);
            ArgumentOutOfRangeException.ThrowIfZero(value.Count());

            field = value.Distinct();
        }
    } = ["atproto"];
}