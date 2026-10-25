// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Collections.ObjectModel;

using idunno.AtProto.Labels;
using idunno.Bluesky.Labeler.Signing;

namespace idunno.Bluesky.Labeler.Host;

/// <summary>Loads host-installed PEM keys and persists active-key selection outside the image.</summary>
internal sealed class PemDirectoryLabelSigningKeyRing : ILabelSigningKeyRing, IDisposable
{
    private readonly ReadOnlyDictionary<string, LabelSigningKey> _keys;
    private readonly string _activeKeyPath;
    private readonly SemaphoreSlim _activationGate = new(1, 1);
    private string _activeKeyId;
    private int _disposed;

    /// <summary>Loads each installed key once and requires a persisted initial key selection.</summary>
    /// <param name="keyDirectory">The directory containing one private-key PEM per key ID.</param>
    /// <param name="activeKeyPath">The writable file containing the active key ID.</param>
    internal PemDirectoryLabelSigningKeyRing(string keyDirectory, string activeKeyPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(activeKeyPath);
        if (!Directory.Exists(keyDirectory))
        {
            throw new DirectoryNotFoundException("The mounted label-signing key directory does not exist.");
        }

        Dictionary<string, LabelSigningKey> keys = new(StringComparer.Ordinal);
        try
        {
            foreach (string path in Directory.EnumerateFiles(keyDirectory, "*.pem", SearchOption.TopDirectoryOnly))
            {
                string keyId = Path.GetFileNameWithoutExtension(path);
                if (string.IsNullOrWhiteSpace(keyId) ||
                    keyId.Length > 128 ||
                    keyId.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.')) ||
                    !keys.TryAdd(keyId, LabelSigningKey.ImportFromPem(File.ReadAllText(path))))
                {
                    throw new InvalidDataException("The mounted key directory contains an invalid or duplicate key ID.");
                }
            }

            if (keys.Count == 0)
            {
                throw new InvalidDataException("The mounted key directory contains no PEM keys.");
            }

            _activeKeyPath = Path.GetFullPath(activeKeyPath);
            _activeKeyId = File.ReadAllText(_activeKeyPath).Trim();
            if (!keys.ContainsKey(_activeKeyId))
            {
                throw new InvalidDataException("The persisted active key ID is not present in the mounted key directory.");
            }

            _keys = new ReadOnlyDictionary<string, LabelSigningKey>(keys);
        }
        catch
        {
            foreach (LabelSigningKey key in keys.Values)
            {
                key.Dispose();
            }

            throw;
        }
    }

    /// <inheritdoc/>
    public string ActiveKeyId
    {
        get
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            return Volatile.Read(ref _activeKeyId);
        }
    }

    /// <inheritdoc/>
    public LabelSigningKey GetSigningKey(string keyId)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        return _keys.TryGetValue(keyId, out LabelSigningKey? key)
            ? key
            : throw new KeyNotFoundException("The requested key ID is not installed on this host.");
    }

    /// <inheritdoc/>
    public string GetPublicKeyMultibase(string keyId) => GetSigningKey(keyId).ExportPublicKeyMultibase();

    /// <inheritdoc/>
    public async Task ActivateAsync(string keyId, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (!_keys.ContainsKey(keyId))
        {
            throw new KeyNotFoundException("The requested key ID is not installed on this host.");
        }

        await _activationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string? directory = Path.GetDirectoryName(_activeKeyPath);
            if (directory is not null)
            {
                Directory.CreateDirectory(directory);
            }

            string temporaryPath = _activeKeyPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                await File.WriteAllTextAsync(temporaryPath, keyId, cancellationToken).ConfigureAwait(false);
                File.Move(temporaryPath, _activeKeyPath, overwrite: true);
                Volatile.Write(ref _activeKeyId, keyId);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }
        finally
        {
            _activationGate.Release();
        }
    }

    /// <summary>Disposes every private key retained by this host-side provider.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            foreach (LabelSigningKey key in _keys.Values)
            {
                key.Dispose();
            }

            _activationGate.Dispose();
        }
    }
}
