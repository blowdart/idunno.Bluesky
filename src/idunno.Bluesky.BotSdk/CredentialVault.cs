// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using idunno.AtProto.Authentication;

namespace idunno.Bluesky.BotSdk;

// The encryption key comes from secrets/configuration, never from the database holding the
// ciphertext. Keep it stable across deployments; losing it makes saved sessions unreadable.
/// <summary>Encrypts saved refresh credentials and binds them to the configured bot account.</summary>
/// <param name="options">The expected DID and externally supplied encryption key.</param>
internal sealed class CredentialVault(BotOptions options)
{
    /// <summary>Protects the refresh token and service without storing the short-lived access token.</summary>
    /// <param name="credentials">The authenticated credentials belonging to the configured DID.</param>
    /// <returns>The nonce, authentication tag, and encrypted refresh data in the durable envelope format.</returns>
    /// <exception cref="InvalidOperationException">The credentials belong to a different account.</exception>
    public byte[] Encrypt(AccessCredentials credentials)
    {
        if (credentials.Did.ToString() != options.Did)
        {
            throw new InvalidOperationException("The authenticated account does not match Bot:Did.");
        }

        // Store only what is needed to refresh: the service and refresh token, not an access token
        // that will soon expire. Encrypting a token does not remove the need to protect backups.
        byte[] plaintext = JsonSerializer.SerializeToUtf8Bytes(
            new SavedCredentials(
    credentials.Service.AbsoluteUri,
    credentials.RefreshToken),
            BotJsonContext.Default.SavedCredentials);

        byte[] key = Convert.FromBase64String(options.CredentialEncryptionKey);

        // GCM needs a fresh nonce for every encryption under the same key. The authentication
        // tag detects tampering; the DID as associated data binds ciphertext to this bot account.
        byte[] nonce = RandomNumberGenerator.GetBytes(12);
        byte[] tag = new byte[16];
        byte[] ciphertext = new byte[plaintext.Length];

        try
        {
            using AesGcm aes = new(key, tag.Length);
            aes.Encrypt(nonce, plaintext, ciphertext, tag, Encoding.UTF8.GetBytes(options.Did));

            // Stored envelope: 12-byte nonce, 16-byte tag, then ciphertext. The nonce and tag are
            // not secrets; decryption still requires the externally supplied 32-byte key.
            return [.. nonce, .. tag, .. ciphertext];
        }
        finally
        {
            // Reduce the lifetime of plaintext/key byte buffers. Immutable strings and SDK
            // credential objects cannot be reliably erased this way, so never log them.
            CryptographicOperations.ZeroMemory(plaintext);
            CryptographicOperations.ZeroMemory(key);
        }
    }

    /// <summary>Authenticates and decrypts saved data into credentials the SDK can refresh.</summary>
    /// <param name="encrypted">The stored envelope produced by <see cref="Encrypt"/>.</param>
    /// <returns>The refresh-only credentials for the saved HTTPS service.</returns>
    /// <exception cref="CryptographicException">The envelope is truncated or its authentication fails.</exception>
    /// <exception cref="JsonException">The decrypted credential document is invalid.</exception>
    /// <exception cref="InvalidDataException">The saved service is not HTTPS.</exception>
    public AtProtoCredential Decrypt(byte[] encrypted)
    {
        if (encrypted.Length < 29)
        {
            throw new CryptographicException("Stored credentials are truncated.");
        }

        byte[] key = Convert.FromBase64String(options.CredentialEncryptionKey);
        byte[] plaintext = new byte[encrypted.Length - 28];

        try
        {
            using AesGcm aes = new(key, 16);

            // Authentication failures are fatal, not permission to silently discard credentials.
            // They can indicate the wrong key, wrong account, damaged storage, or tampering.
            aes.Decrypt(
                encrypted.AsSpan(0, 12),
                encrypted.AsSpan(28),
                encrypted.AsSpan(12, 16),
                plaintext,
                Encoding.UTF8.GetBytes(options.Did));

            SavedCredentials saved = JsonSerializer.Deserialize(plaintext, BotJsonContext.Default.SavedCredentials)
                ?? throw new JsonException("Stored credentials are null.");

            Uri service = new(saved.Service);

            if (service.Scheme != "https")
            {
                throw new InvalidDataException("Stored credentials must belong to an HTTPS service.");
            }

            return AtProtoCredential.Create(service, AuthenticationType.UsernamePassword, refreshToken: saved.RefreshToken);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

}
