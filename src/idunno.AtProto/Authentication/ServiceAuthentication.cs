// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using idunno.AtProto.Repo;

using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

#pragma warning disable IDE0130 // Authentication features share the protocol's root namespace.
namespace idunno.AtProto;
#pragma warning restore IDE0130

/// <summary>
/// Verifies short-lived AT Protocol service authentication tokens.
/// </summary>
public static class ServiceAuthentication
{
    private const int MaximumTokenLength = 8192;
    private static readonly TimeSpan s_defaultMaximumLifetime = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Verifies a service token's signature, issuer, audience, method and lifetime.
    /// </summary>
    /// <param name="token">The compact signed JWT.</param>
    /// <param name="audience">The exact destination service reference.</param>
    /// <param name="method">The exact XRPC method receiving the token.</param>
    /// <param name="httpClient">The HTTP client used to resolve the issuer's DID document.</param>
    /// <param name="timeProvider">An optional clock used to validate the lifetime.</param>
    /// <param name="maximumLifetime">The maximum accepted token lifetime. The default is 15 minutes.</param>
    /// <param name="cancellationToken">A cancellation token for DID resolution.</param>
    /// <returns>The verified identity and identifiers for replay protection.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maximumLifetime"/> is zero or negative.</exception>
    /// <exception cref="InvalidDataException">The token, claims, DID document or signature is invalid.</exception>
    /// <exception cref="HttpRequestException">DID resolution fails at the transport layer.</exception>
    /// <exception cref="OperationCanceledException">The operation is canceled.</exception>
    /// <remarks>
    /// <para>Requires an integer lifetime no greater than <paramref name="maximumLifetime"/>, defaults to 15 minutes,
    /// allows five seconds of clock skew, and accepts only
    /// ES256 or ES256K signatures made by the issuer's matching <c>#atproto</c> key.</para>
    /// <para>This method does not prevent replay. The caller must atomically reserve the issuer and token identifier
    /// and the token hash before accepting a request, retaining them through the expiry plus clock skew.</para>
    /// </remarks>
    public static async Task<ServiceAuthenticationResult> VerifyAsync(
        string token,
        AtProtoServiceReference audience,
        Nsid method,
        HttpClient httpClient,
        TimeProvider? timeProvider = null,
        TimeSpan? maximumLifetime = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(audience);
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(httpClient);
        cancellationToken.ThrowIfCancellationRequested();
        TimeSpan lifetimeLimit = maximumLifetime ?? s_defaultMaximumLifetime;
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lifetimeLimit, TimeSpan.Zero);

        try
        {
            if (token.Length is 0 or > MaximumTokenLength)
            {
                throw new InvalidDataException("The service token exceeds the supported size or is empty.");
            }

            string[] parts = token.Split('.');
            if (parts.Length != 3)
            {
                throw new InvalidDataException("The service token must be a compact signed JWT.");
            }

            byte[] headerBytes = DecodeSegment(parts[0]);
            byte[] payloadBytes = DecodeSegment(parts[1]);
            byte[] signature = DecodeSegment(parts[2]);
            if (signature.Length != 64)
            {
                throw new InvalidDataException("The service token signature must contain 64 bytes.");
            }

            using JsonDocument headerDocument = JsonDocument.Parse(headerBytes, new JsonDocumentOptions { MaxDepth = 16 });
            using JsonDocument payloadDocument = JsonDocument.Parse(payloadBytes, new JsonDocumentOptions { MaxDepth = 16 });
            JsonElement header = headerDocument.RootElement;
            JsonElement payload = payloadDocument.RootElement;
            ValidateObject(header);
            ValidateObject(payload);

            if (ReadString(header, "typ") != "JWT" ||
                ReadString(header, "alg") is not ("ES256" or "ES256K") ||
                (header.TryGetProperty("kid", out JsonElement kid) && (kid.ValueKind != JsonValueKind.String || kid.GetString() != "#atproto")) ||
                header.TryGetProperty("jku", out _) || header.TryGetProperty("x5u", out _) ||
                header.TryGetProperty("jwk", out _) || header.TryGetProperty("crit", out _) ||
                header.TryGetProperty("b64", out _))
            {
                throw new InvalidDataException("The service token contains an unsupported JOSE header.");
            }

            JsonWebTokenHandler handler = new() { MaximumTokenSizeInBytes = MaximumTokenLength };
            JsonWebToken jwt = handler.ReadJsonWebToken(token);
            if (jwt.IsEncrypted ||
                ReadString(payload, "aud") != audience.ToString() ||
                ReadString(payload, "lxm") != method.ToString())
            {
                throw new InvalidDataException("The service token audience or method does not match the request.");
            }

            string issuer = ReadString(payload, "iss");
            if (!Did.TryParse(issuer, out Did? did))
            {
                throw new InvalidDataException("The service token issuer must be a bare DID.");
            }

            string tokenId = ReadString(payload, "jti");
            if (string.IsNullOrWhiteSpace(tokenId) || tokenId.Length > 256)
            {
                throw new InvalidDataException("The service token identifier must contain between 1 and 256 nonblank characters.");
            }

            DateTimeOffset issuedAt = ReadTimestamp(payload, "iat");
            DateTimeOffset expiresAt = ReadTimestamp(payload, "exp");
            DateTimeOffset now = (timeProvider ?? TimeProvider.System).GetUtcNow();
            ValidateLifetime(issuedAt, expiresAt, now, lifetimeLimit);

            DidDocument? document = await IdentityResolution.ResolveDidDocumentAsync(
                did: did,
                httpClient: httpClient,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            SigningKey key = SigningKeyVerifier.GetKey(document, did, SigningKeyVerifier.RepositorySigningKeyFragment);
            if (key.JwtAlgorithm != jwt.Alg)
            {
                throw new InvalidDataException("The service token algorithm does not match the issuer's signing key.");
            }

            key.Verify(Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"), signature, "service token");
            ValidateLifetime(issuedAt, expiresAt, (timeProvider ?? TimeProvider.System).GetUtcNow(), lifetimeLimit);

            return new ServiceAuthenticationResult(
                issuer: did,
                audience: audience,
                tokenId: tokenId,
                expiresAt: expiresAt,
                tokenHash: Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(token))));
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or FormatException or CryptographicException or SecurityTokenException)
        {
            throw new InvalidDataException("The service token could not be verified.", exception);
        }
    }

    /// <summary>
    /// Decodes a canonical, unpadded base64url JWT segment.
    /// </summary>
    private static byte[] DecodeSegment(string segment)
    {
        if (segment.Length == 0 || segment.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_'))
        {
            throw new InvalidDataException("The service token contains an invalid base64url segment.");
        }

        string padded = segment.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight((padded.Length + 3) / 4 * 4, '=');
        byte[] bytes = Convert.FromBase64String(padded);
        if (Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_') != segment)
        {
            throw new InvalidDataException("The service token contains a noncanonical base64url segment.");
        }

        return bytes;
    }

    /// <summary>
    /// Rejects duplicate properties and invalid Unicode anywhere in a JWT JSON object.
    /// </summary>
    private static void ValidateObject(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("JWT headers and claims must be JSON objects.");
        }

        try
        {
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw new InvalidDataException("The service token contains duplicate JSON properties.");
                }

                ValidateChildren(property.Value);
            }
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidDataException("The service token contains invalid JSON text.", exception);
        }
    }

    /// <summary>
    /// Traverses nested JSON values to validate object properties and force Unicode decoding of every string.
    /// </summary>
    private static void ValidateChildren(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            ValidateObject(element);
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement child in element.EnumerateArray())
            {
                ValidateChildren(child);
            }
        }
        else if (element.ValueKind == JsonValueKind.String)
        {
            _ = element.GetString();
        }
    }

    /// <summary>
    /// Reads a required scalar string without coercing JSON values.
    /// </summary>
    private static string ReadString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement value) || value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException($"The service token requires a scalar '{name}' string.");
        }

        return value.GetString()!;
    }

    /// <summary>
    /// Reads a required finite integer timestamp within the supported date range.
    /// </summary>
    private static DateTimeOffset ReadTimestamp(JsonElement payload, string name)
    {
        if (!payload.TryGetProperty(name, out JsonElement value) ||
            value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out long seconds))
        {
            throw new InvalidDataException($"The service token requires an integer '{name}' timestamp.");
        }

        return DateTimeOffset.FromUnixTimeSeconds(seconds);
    }

    /// <summary>
    /// Enforces the bounded lifetime before and after DID resolution.
    /// </summary>
    private static void ValidateLifetime(
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt,
        DateTimeOffset now,
        TimeSpan maximumLifetime)
    {
        TimeSpan lifetime = expiresAt - issuedAt;
        if (lifetime <= TimeSpan.Zero || lifetime > maximumLifetime ||
            issuedAt - now > TimeSpan.FromSeconds(5) || now - expiresAt > TimeSpan.FromSeconds(5))
        {
            throw new InvalidDataException("The service token lifetime is invalid or has expired.");
        }
    }
}
