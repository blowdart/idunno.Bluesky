// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Microsoft.IdentityModel.Tokens;

namespace idunno.AtProto.Authentication;

/// <summary>
/// Represents the ES256 private key a confidential OAuth client authenticates itself to authorization servers with.
/// </summary>
/// <remarks>
/// <para>
///   Confidential clients sign a JWT client assertion, as described in RFC 7523, for each pushed authorization, token and
///   refresh request, and publish the matching public key in their client metadata. AT Protocol authorization servers
///   bind a session to the key that started it, so a session ends if its key is removed from the client metadata.
/// </para>
/// <para>
///   The client authentication key is shared by every session and is separate from the per-session DPoP proof key.
///   Keep the private key secret.
/// </para>
/// </remarks>
public sealed class OAuthClientSigningKey
{
    /// <summary>
    /// The JSON Web Algorithm client assertions are signed with.
    /// </summary>
    public const string Algorithm = "ES256";

    /// <summary>
    /// The JSON Web Key curve name of a client signing key.
    /// </summary>
    public const string Curve = "P-256";

    /// <summary>
    /// The RFC 7523 client assertion type for a JWT client assertion.
    /// </summary>
    internal const string JwtBearerClientAssertionType = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer";

    private static readonly TimeSpan s_assertionLifetime = TimeSpan.FromMinutes(1);

    private readonly ECParameters _parameters;

    private OAuthClientSigningKey(ECParameters parameters, string? keyId)
    {
        _parameters = parameters;
        X = Base64UrlEncode(parameters.Q.X!);
        Y = Base64UrlEncode(parameters.Q.Y!);
        KeyId = keyId ?? ComputeThumbprint(X, Y);
    }

    /// <summary>
    /// Gets the key identifier placed in the <c>kid</c> header of client assertions and in the published public key.
    /// </summary>
    /// <value>The key identifier. The default is the RFC 7638 SHA-256 thumbprint of the public key.</value>
    public string KeyId { get; }

    /// <summary>
    /// Gets the base64url encoded x coordinate of the public key.
    /// </summary>
    public string X { get; }

    /// <summary>
    /// Gets the base64url encoded y coordinate of the public key.
    /// </summary>
    public string Y { get; }

    /// <summary>
    /// Generates a new ECDSA P-256 client signing key.
    /// </summary>
    /// <param name="keyId">The key identifier, or <see langword="null"/> to use the RFC 7638 thumbprint.</param>
    /// <returns>A new private signing key.</returns>
    /// <exception cref="ArgumentException"><paramref name="keyId"/> is empty or white space.</exception>
    /// <remarks>
    /// <para>Retain the returned key for the lifetime of its sessions. This method does not persist the private key.</para>
    /// </remarks>
    public static OAuthClientSigningKey Generate(string? keyId = null)
    {
        if (keyId is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(keyId);
        }

        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        return new OAuthClientSigningKey(key.ExportParameters(includePrivateParameters: true), keyId);
    }

    /// <summary>
    /// Imports an ECDSA P-256 private signing key from a JSON Web Key.
    /// </summary>
    /// <param name="jwk">The private JWK, including its public coordinates and private scalar.</param>
    /// <param name="keyId">The identifier to use instead of the identifier in <paramref name="jwk"/>, or <see langword="null"/> to retain it or compute a thumbprint.</param>
    /// <returns>A validated private client signing key.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="jwk"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="jwk"/> is not a valid ES256 private signing key, or its key identifier is empty or white space.</exception>
    public static OAuthClientSigningKey FromJwk(string jwk, string? keyId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jwk);
        if (keyId is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(keyId);
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(jwk);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                document.RootElement.EnumerateObject().GroupBy(property => property.Name, StringComparer.Ordinal).Any(group => group.Count() > 1))
            {
                throw new ArgumentException("The JWK must be an object with unique member names.", nameof(jwk));
            }

            JsonWebKey key = new(jwk);
            if (key.Kty != "EC" || key.Crv != Curve ||
                (key.Alg is not null && key.Alg != Algorithm) ||
                (key.Use is not null && key.Use != "sig") ||
                (document.RootElement.TryGetProperty("key_ops", out _) && (!key.KeyOps.Contains("sign", StringComparer.Ordinal) ||
                    key.KeyOps.Any(operation => operation is not ("sign" or "verify")))))
            {
                throw new ArgumentException("The JWK must be an EC P-256 key permitted to sign with ES256.", nameof(jwk));
            }

            string? effectiveKeyId = keyId ?? key.Kid;
            if (effectiveKeyId is not null && string.IsNullOrWhiteSpace(effectiveKeyId))
            {
                throw new ArgumentException("The JWK key identifier must not be empty or white space.", nameof(jwk));
            }

            ECParameters parameters = new()
            {
                Curve = ECCurve.NamedCurves.nistP256,
                Q = new ECPoint { X = DecodeJwkParameter(key.X), Y = DecodeJwkParameter(key.Y) },
                D = DecodeJwkParameter(key.D)
            };
            using ECDsa privateKey = ECDsa.Create(parameters);
            using ECDsa publicKey = ECDsa.Create(new ECParameters { Curve = parameters.Curve, Q = parameters.Q });
            byte[] challenge = RandomNumberGenerator.GetBytes(32);
            byte[] signature = privateKey.SignData(challenge, HashAlgorithmName.SHA256);
            if (!publicKey.VerifyData(challenge, signature, HashAlgorithmName.SHA256))
            {
                throw new ArgumentException("The JWK public coordinates do not match its private key.", nameof(jwk));
            }

            return new OAuthClientSigningKey(privateKey.ExportParameters(includePrivateParameters: true), effectiveKeyId);
        }
        catch (Exception e) when (e is JsonException or CryptographicException or FormatException)
        {
            throw new ArgumentException("The JWK does not contain a valid ECDSA P-256 private key.", nameof(jwk), e);
        }
    }

    /// <summary>
    /// Exports the public signing key as a JWK suitable for confidential-client metadata.
    /// </summary>
    /// <returns>A public-only JWK containing the key identifier and ES256 signing algorithm, never private key material.</returns>
    public string ToPublicJwk() => Encoding.UTF8.GetString(WriteJson(writer =>
    {
        writer.WriteString("kty", "EC");
        writer.WriteString("crv", Curve);
        writer.WriteString("alg", Algorithm);
        writer.WriteString("use", "sig");
        writer.WriteString("kid", KeyId);
        writer.WriteString("x", X);
        writer.WriteString("y", Y);
    }));

    /// <summary>
    /// Decodes a canonical 256-bit unsigned JWK coordinate or private scalar.
    /// </summary>
    private static byte[] DecodeJwkParameter(string? value)
    {
        if (value is null || value.Length != 43 || value.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('-' or '_')))
        {
            throw new FormatException("The JWK must contain 32-byte base64url x, y and d values.");
        }

        byte[] decoded = Base64UrlEncoder.DecodeBytes(value);
        if (Base64UrlEncoder.Encode(decoded) != value)
        {
            throw new FormatException("The JWK parameters must use canonical base64url encoding.");
        }

        return decoded;
    }

    /// <summary>
    /// Creates a client signing key from an ECDSA P-256 private key encoded in <paramref name="pem"/>.
    /// </summary>
    /// <param name="pem">The PEM text of an unencrypted PKCS#8 or SEC 1 private key.</param>
    /// <param name="keyId">The key identifier to publish, or <see langword="null"/> to use the RFC 7638 thumbprint of the public key.</param>
    /// <returns>A client signing key for the private key in <paramref name="pem"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="pem"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="pem"/> is empty, white space, or does not contain an unencrypted ECDSA P-256 private key,
    /// or <paramref name="keyId"/> is empty or white space.
    /// </exception>
    public static OAuthClientSigningKey FromPem(string pem, string? keyId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pem);

        if (keyId is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(keyId);
        }

        ECParameters parameters;

        try
        {
            using ECDsa key = ECDsa.Create();
            key.ImportFromPem(pem);
            parameters = key.ExportParameters(includePrivateParameters: true);
        }
        catch (Exception e) when (e is ArgumentException or CryptographicException)
        {
            throw new ArgumentException("The PEM does not contain an unencrypted ECDSA private key.", nameof(pem), e);
        }

        if (parameters.D is null || !IsP256(parameters.Curve))
        {
            throw new ArgumentException("The PEM does not contain an ECDSA P-256 private key.", nameof(pem));
        }

        return new OAuthClientSigningKey(parameters, keyId);
    }

    /// <summary>
    /// Creates a client signing key from a file containing an ECDSA P-256 private key in PEM format.
    /// </summary>
    /// <param name="path">The path of a file containing an unencrypted PKCS#8 or SEC 1 private key.</param>
    /// <param name="keyId">The key identifier to publish, or <see langword="null"/> to use the RFC 7638 thumbprint of the public key.</param>
    /// <returns>A client signing key for the private key in the file at <paramref name="path"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="path"/> is empty or white space, the file does not contain an unencrypted ECDSA P-256 private key,
    /// or <paramref name="keyId"/> is empty or white space.
    /// </exception>
    /// <exception cref="FileNotFoundException">The file at <paramref name="path"/> does not exist.</exception>
    /// <exception cref="IOException">The file at <paramref name="path"/> cannot be read.</exception>
    /// <exception cref="UnauthorizedAccessException">The caller does not have permission to read the file at <paramref name="path"/>.</exception>
    public static OAuthClientSigningKey FromPemFile(string path, string? keyId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"The client signing key file '{path}' was not found.", path);
        }

        return FromPem(File.ReadAllText(path), keyId);
    }

    /// <summary>
    /// Creates a signed JWT client assertion for a request to an authorization server.
    /// </summary>
    /// <param name="clientId">The client ID, used as the issuer and subject of the assertion.</param>
    /// <param name="authority">The authorization server the assertion is for.</param>
    /// <param name="now">The current time.</param>
    /// <param name="clockSkew">The amount by which the issued-at timestamp is backdated.</param>
    /// <returns>The compact serialized client assertion.</returns>
    /// <remarks>
    /// <para>
    ///   AT Protocol authorization server issuers are origins, so the audience is the origin of <paramref name="authority"/>.
    ///   Every assertion carries a new random <c>jti</c>, because authorization servers reject a replayed one.
    ///   The issued-at timestamp is backdated by <paramref name="clockSkew"/> to accommodate small clock differences,
    ///   while the assertion expires one minute after <paramref name="now"/>.
    /// </para>
    /// </remarks>
    internal string CreateClientAssertion(string clientId, Uri authority, DateTimeOffset now, TimeSpan clockSkew)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentNullException.ThrowIfNull(authority);

        long issuedAt = now.Subtract(clockSkew).ToUnixTimeSeconds();
        long expiresAt = now.Add(s_assertionLifetime).ToUnixTimeSeconds();

        byte[] header = WriteJson(writer =>
        {
            writer.WriteString("alg", Algorithm);
            writer.WriteString("typ", "JWT");
            writer.WriteString("kid", KeyId);
        });

        byte[] payload = WriteJson(writer =>
        {
            writer.WriteString("iss", clientId);
            writer.WriteString("sub", clientId);
            writer.WriteString("aud", GetAudience(authority));
            writer.WriteString("jti", Base64UrlEncode(RandomNumberGenerator.GetBytes(16)));
            writer.WriteNumber("iat", issuedAt);
            writer.WriteNumber("exp", expiresAt);
        });

        string signingInput = $"{Base64UrlEncode(header)}.{Base64UrlEncode(payload)}";

        byte[] signature;
        using (ECDsa key = ECDsa.Create(_parameters))
        {
            // SignData produces the IEEE P1363 fixed length r || s signature JWS requires.
            signature = key.SignData(Encoding.ASCII.GetBytes(signingInput), HashAlgorithmName.SHA256);
        }

        return $"{signingInput}.{Base64UrlEncode(signature)}";
    }

    /// <summary>
    /// Gets the client assertion audience for an authorization server.
    /// </summary>
    /// <param name="authority">The authorization server.</param>
    /// <returns>The origin of <paramref name="authority"/>, which is its AT Protocol issuer identifier.</returns>
    internal static string GetAudience(Uri authority) => authority.GetLeftPart(UriPartial.Authority);

    private static bool IsP256(ECCurve curve)
    {
        if (!curve.IsNamed)
        {
            return false;
        }

        string? oid = curve.Oid.Value;
        string? friendlyName = curve.Oid.FriendlyName;

        return oid == "1.2.840.10045.3.1.7" ||
            friendlyName is "nistP256" or "ECDSA_P256" or "secp256r1" or "prime256v1";
    }

    private static string ComputeThumbprint(string x, string y)
    {
        // RFC 7638 requires the required members in lexicographic order with no whitespace.
        byte[] canonical = WriteJson(writer =>
        {
            writer.WriteString("crv", "P-256");
            writer.WriteString("kty", "EC");
            writer.WriteString("x", x);
            writer.WriteString("y", y);
        });

        return Base64UrlEncode(SHA256.HashData(canonical));
    }

    private static byte[] WriteJson(Action<Utf8JsonWriter> writeProperties)
    {
        ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();
            writeProperties(writer);
            writer.WriteEndObject();
        }

        return buffer.WrittenSpan.ToArray();
    }

    private static string Base64UrlEncode(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
