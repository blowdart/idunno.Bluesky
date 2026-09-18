// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;

using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace idunno.AtProto.Authentication;

/// <summary>
/// Encapsulates the credentials and supporting values necessary to call an authenticated API.
/// </summary>
public class AccessCredentials : RefreshCredential, IAccessCredential
{
#if NET9_0_OR_GREATER
    private readonly Lock _accessCredentialsLock = new();
#else
    private readonly object _accessCredentialsLock = new();
#endif

    private string _accessToken;
    private DateTimeOffset _expiresOn;
    private Did _did;

    /// <summary>
    /// Creates a new instance of <see cref="AccessCredentials"/> with the specified <paramref name="accessJwt"/> and <paramref name="refreshToken"/>.
    /// </summary>
    /// <param name="service">The <see cref="Uri"/> of the service the credentials were issued from.</param>
    /// <param name="authenticationType">The type of authentication used to acquire the credentials.</param>
    /// <param name="accessJwt">A string representation of the JWT to use when making authenticated access requests.</param>
    /// <param name="refreshToken">A string representation of the token to use when a new access token is required.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="service"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="accessJwt"/> or <paramref name="refreshToken"/> is <see langword="null"/> or whitespace,
    /// or when <paramref name="accessJwt"/> cannot be parsed as a JWT carrying a subject which is a valid <see cref="AtProto.Did"/>.
    /// </exception>
    public AccessCredentials(Uri service, AuthenticationType authenticationType, string accessJwt, string refreshToken) : base(service, authenticationType, refreshToken)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentException.ThrowIfNullOrWhiteSpace(accessJwt);
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);

        (_did, _expiresOn) = ExtractJwtProperties(accessJwt);
        _accessToken = accessJwt;
    }

    /// <summary>
    /// Gets a string representation of the JWT to use when making authenticated access requests.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Thrown when setting the value and the value is <see langword="null"/> or whitespace, or cannot be parsed as a JWT
    /// carrying a subject which is a valid <see cref="AtProto.Did"/>.
    /// </exception>
    /// <remarks>
    /// <para>
    ///   Setting this also updates <see cref="Did"/> and <see cref="ExpiresOn"/> from the new token. The new values are
    ///   extracted before any of the three are published, so a token whose subject cannot be read is rejected without
    ///   leaving the credential holding a token paired with the identity or the expiry of the one it replaced.
    /// </para>
    /// </remarks>
    public string AccessJwt
    {
        get
        {
            lock (_accessCredentialsLock)
            {
                return _accessToken;
            }
        }

        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);

            (Did did, DateTimeOffset expiresOn) = ExtractJwtProperties(value);

            lock (_accessCredentialsLock)
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
    ///   Read under the same lock the <see cref="AccessJwt"/> setter writes it under, so a caller cannot observe the
    ///   expiry of one token alongside another, or a value part written by a refresh running on another thread.
    /// </para>
    /// </remarks>
    public DateTimeOffset ExpiresOn
    {
        get
        {
            lock (_accessCredentialsLock)
            {
                return _expiresOn;
            }
        }
    }

    /// <summary>
    /// Gets the <see cref="AtProto.Did"/> the access token was issued for.
    /// </summary>
    public Did Did
    {
        get
        {
            lock (_accessCredentialsLock)
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

    /// <summary>
    /// Extracts the DID and expiration date from the specified jwt.
    /// </summary>
    /// <param name="jwt">A string representation of the jwt to extract the properties from</param>.
    /// <returns>The DID and expiration date the jwt carries.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="jwt"/> is <see langword="null"/> or whitespace, cannot be parsed as a JWT, or does not
    /// carry a subject which is a valid <see cref="AtProto.Did"/>.
    /// </exception>
    /// <remarks>
    /// <para>
    ///   A token which cannot be parsed, or whose subject is not a DID, is reported as an <see cref="ArgumentException"/>
    ///   naming the parameter it came from. Both are properties of the value being supplied, so letting the underlying
    ///   <see cref="SecurityTokenMalformedException"/> or the <see cref="AtProto.Did"/> constructor's own exception escape
    ///   would report a failure to validate an argument as something the caller cannot relate to the value it passed.
    /// </para>
    /// </remarks>
    private static (Did did, DateTimeOffset expiresOn) ExtractJwtProperties(string jwt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jwt);

        JsonWebToken token;

        try
        {
            token = new JsonWebToken(jwt);
        }
        catch (ArgumentException ex)
        {
            // SecurityTokenMalformedException derives from ArgumentException, so this catches both an argument the token
            // reader rejects outright and a value which is not a well formed JWT.
            throw new ArgumentException("Value could not be parsed as a JWT.", nameof(jwt), ex);
        }

        if (!Did.TryParse(token.Subject, out Did? did))
        {
            throw new ArgumentException("Value does not contain a subject which is a valid DID.", nameof(jwt));
        }

        return (did, DateTime.SpecifyKind(token.ValidTo, DateTimeKind.Utc));
    }
}