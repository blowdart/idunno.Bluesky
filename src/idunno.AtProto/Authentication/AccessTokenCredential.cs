// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;

using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace idunno.AtProto.Authentication;

/// <summary>
/// Encapsulates the minimum credentials to call an authenticated API.
/// </summary>
public sealed class AccessTokenCredential : AtProtoCredential, IAccessCredential
{
#if NET9_0_OR_GREATER
    private readonly Lock _accessTokenCredentialLock = new();
#else
    private readonly object _accessTokenCredentialLock = new();
#endif

    private string _accessJwt;
    private DateTimeOffset _expiresOn;
    private Did _did;

    private static readonly Uri s_invalidServiceUri = new("https://invalid.invalid");

    /// <summary>
    /// Creates a new instance of <see cref="AccessCredentials"/> with the specified <paramref name="jwt"/>.
    /// </summary>
    /// <param name="jwt">A string representation of the JWT to use when making authenticated access requests.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="jwt"/> is <see langword="null"/> or empty, cannot be parsed as a JSON web token, or does not carry
    /// a subject which is a valid <see cref="Did"/>.
    /// </exception>
    public AccessTokenCredential(string jwt) : this(s_invalidServiceUri, jwt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jwt);
    }

    /// <summary>
    /// Creates a new instance of <see cref="AccessCredentials"/> with the specified <paramref name="jwt"/>.
    /// </summary>
    /// <param name="service">The <see cref="Uri"/> of the service the credentials were issued from.</param>
    /// <param name="jwt">A string representation of the JWT to use when making authenticated access requests.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="service"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="jwt"/> is <see langword="null"/> or empty, cannot be parsed as a JSON web token, or does not carry
    /// a subject which is a valid <see cref="Did"/>.
    /// </exception>
    public AccessTokenCredential(Uri service, string jwt) : base(service, AuthenticationType.Unknown)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentException.ThrowIfNullOrWhiteSpace(jwt);

        (_did, _expiresOn) = ExtractJwtProperties(jwt);
        _accessJwt = jwt;
    }

    /// <summary>
    /// Gets a string representation of the JWT to use when making authenticated access requests.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Thrown when setting the value and the value is <see langword="null"/> or whitespace, cannot be parsed as a JSON web token,
    /// or does not carry a subject which is a valid <see cref="Did"/>.
    /// </exception>
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
            lock (_accessTokenCredentialLock)
            {
                return _accessJwt;
            }
        }

        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);

            (Did did, DateTimeOffset expiresOn) = ExtractJwtProperties(value);

            lock (_accessTokenCredentialLock)
            {
                _accessJwt = value;
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
    ///   expiry of one token alongside another.
    /// </para>
    /// </remarks>
    public DateTimeOffset ExpiresOn
    {
        get
        {
            lock (_accessTokenCredentialLock)
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
    ///   Read under the same lock the <see cref="AccessJwt"/> setter writes it under, so a caller cannot observe the
    ///   subject of one token alongside another.
    /// </para>
    /// </remarks>
    public Did Did
    {
        get
        {
            lock (_accessTokenCredentialLock)
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
    /// Extracts the <see cref="AtProto.Did"/> and expiry from the specified <paramref name="jwt"/>.
    /// </summary>
    /// <param name="jwt">The JWT to extract the properties from.</param>
    /// <returns>The <see cref="AtProto.Did"/> and expiry the <paramref name="jwt"/> carries.</returns>
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