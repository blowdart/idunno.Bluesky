// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;

using Microsoft.IdentityModel.JsonWebTokens;

namespace idunno.AtProto.Authentication;

/// <summary>
/// Encapsulates the credential to call an authenticated service API.
/// Service credentials only have an Access JWT, and have no refresh token.
/// </summary>

public class ServiceCredential : AtProtoCredential, IAccessCredential
{
#if NET9_0_OR_GREATER
    private readonly Lock _serviceCredentialLock = new();
#else
    private readonly object _serviceCredentialLock = new();
#endif

    private string _accessToken;
    private DateTimeOffset _expiresOn;
    private Did _did;

    /// <summary>
    /// Creates a new instance of <see cref="AccessCredentials"/> with the specified <paramref name="accessJwt"/>.
    /// </summary>
    /// <param name="service">The <see cref="Uri"/> of the service the credentials were issued from.</param>
    /// <param name="accessJwt">A string representation of the JWT to use when making authenticated access requests.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="service"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="accessJwt"/> is <see langword="null"/> or whitespace, or carries no audience.</exception>
    public ServiceCredential(Uri service, string accessJwt) : base(service, AuthenticationType.Service)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentException.ThrowIfNullOrWhiteSpace(accessJwt);

        (_did, _expiresOn) = ExtractJwtProperties(accessJwt);
        _accessToken = accessJwt;
    }

    /// <summary>
    /// Gets a string representation of the JWT to use when making authenticated access requests.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when setting the value and the value is <see langword="null"/> or whitespace, or carries no audience.</exception>
    /// <remarks>
    /// <para>
    ///   Setting this also updates <see cref="Did"/> and <see cref="ExpiresOn"/> from the new token. The three are published
    ///   together, so a token is never left paired with the identity or the expiry of the token it replaced.
    /// </para>
    /// </remarks>
    public string AccessJwt
    {
        get
        {
            lock (_serviceCredentialLock)
            {
                return _accessToken;
            }
        }

        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);

            (Did did, DateTimeOffset expiresOn) = ExtractJwtProperties(value);

            lock (_serviceCredentialLock)
            {
                _accessToken = value;
                _did = did;
                _expiresOn = expiresOn;
            }
        }
    }

    /// <summary>
    /// Gets the <see cref="DateTimeOffset"/> the <see cref="AccessJwt"/> expires on.
    /// </summary>
    /// <remarks>
    /// <para>Identifies the expiration time on or after which the JWT MUST NOT be accepted for processing. See: https://datatracker.ietf.org/doc/html/rfc7519#section-4.1.4.</para>
    /// <para>If the 'exp' claim is not found, then <see cref="DateTimeOffset.MinValue">MinValue</see> is returned.</para>
    /// <para>
    ///   The token, its subject and its expiry are published together by the <see cref="AccessJwt"/> setter, so this
    ///   never returns a value part written by a refresh running on another thread. Each property takes the lock
    ///   separately, so reading this alongside <see cref="AccessJwt"/> can still straddle a refresh and pair the
    ///   expiry of one token with another.
    /// </para>
    /// </remarks>
    public DateTimeOffset ExpiresOn
    {
        get
        {
            lock (_serviceCredentialLock)
            {
                return _expiresOn;
            }
        }
    }

    /// <summary>
    /// Gets the <see cref="AtProto.Did"/> the access token was issued for.
    /// </summary>
    /// <remarks>
    /// <para>
    ///   The token, its subject and its expiry are published together by the <see cref="AccessJwt"/> setter, so this
    ///   never returns a value part written by a refresh running on another thread. Each property takes the lock
    ///   separately, so reading this alongside <see cref="AccessJwt"/> can still straddle a refresh and pair the
    ///   audience of one token with another.
    /// </para>
    /// </remarks>
    public Did Did
    {
        get
        {
            lock (_serviceCredentialLock)
            {
                return _did;
            }
        }
    }

    /// <summary>
    /// Add authentication headers to the specified <paramref name="httpRequestMessage"/>.
    /// </summary>
    /// <param name="httpRequestMessage">The <see cref="HttpRequestMessage"/> to add authentication headers to.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="httpRequestMessage"/> is <see langword="null"/>.</exception>
    public override void SetAuthenticationHeaders(HttpRequestMessage httpRequestMessage)
    {
        ArgumentNullException.ThrowIfNull(httpRequestMessage);

        httpRequestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AccessJwt);
    }

    private static (Did did, DateTimeOffset expiresOn) ExtractJwtProperties(string jwt)
    {
        JsonWebToken token = new(jwt);

        // Service JWTs don't have subjects, but they do have audiences which is equivalent for services.

        if (token.Audiences is null || !token.Audiences.Any())
        {
            throw new ArgumentException("Service token carries no audience.", nameof(jwt));
        }

        return (new Did(token.Audiences.First()), DateTime.SpecifyKind(token.ValidTo, DateTimeKind.Utc));
    }
}