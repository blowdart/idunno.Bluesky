// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Collections.ObjectModel;

using idunno.AtProto.Labels;
using idunno.Bluesky.Labeler.Maintenance;
using idunno.Bluesky.Labeler.Signing;

namespace idunno.Bluesky.Labeler.Host;

/// <summary>Loads host-installed PEM keys and persists active-key selection outside the image.</summary>
internal sealed class PemDirectoryLabelSigningKeyRing : ILabelSigningKeyRing, IDisposable
{
    private readonly ReadOnlyDictionary<string, LabelSigningKey> _keys;
    private readonly string _activeKeyPath;
    private readonly SemaphoreSlim _activationGate = new(1, 1);
    private ILabelSigningKeySelectionStore? _selectionStore;
    private ILabelMaintenanceStore? _maintenance;
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

    /// <summary>Seeds database selection from the legacy file or recovers the authoritative database selection.</summary>
    /// <param name="selectionStore">The initialized database's selection provider.</param>
    /// <param name="maintenance">The same database's maintenance provider.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    internal async Task InitializeAsync(
        ILabelSigningKeySelectionStore selectionStore,
        ILabelMaintenanceStore maintenance,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        await _activationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_selectionStore is not null)
            {
                throw new InvalidOperationException("The signing key ring is already initialized.");
            }

            LabelSigningKeySelection? selection = await selectionStore.GetSigningKeySelectionAsync(cancellationToken).ConfigureAwait(false);
            selection ??= await selectionStore.InitializeSigningKeySelectionAsync(
                _activeKeyId,
                GetPublicKeyMultibase(_activeKeyId),
                cancellationToken).ConfigureAwait(false);
            if (GetPublicKeyMultibase(selection.KeyId) != selection.PublicKey)
            {
                throw new InvalidOperationException("The installed private key does not match the persisted selection.");
            }

            await PersistActiveKeyAsync(selection.KeyId, cancellationToken).ConfigureAwait(false);
            _maintenance = maintenance;
            _selectionStore = selectionStore;
        }
        finally
        {
            _activationGate.Release();
        }
    }

    /// <inheritdoc/>
    public string ActiveKeyId
    {
        get
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            if (_selectionStore is null)
            {
                throw new InvalidOperationException("Initialize the signing key ring before use.");
            }

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
            ILabelSigningKeySelectionStore store = _selectionStore
                ?? throw new InvalidOperationException("Initialize the signing key ring before activation.");
            LabelMaintenanceState rotation = await _maintenance!.GetMaintenanceStateAsync(cancellationToken).ConfigureAwait(false);
            if (rotation.OperationId is null || rotation.PreviousKeyId is null)
            {
                throw new InvalidOperationException("Signing-key activation requires an existing rotation.");
            }

            LabelSigningKeySelection selected = await store.ActivateSigningKeySelectionAsync(
                operationId: rotation.OperationId,
                previousKeyId: rotation.PreviousKeyId,
                keyId: keyId,
                publicKey: GetPublicKeyMultibase(keyId),
                cancellationToken: cancellationToken).ConfigureAwait(false);
            await PersistActiveKeyAsync(selected.KeyId, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _activationGate.Release();
        }
    }

    /// <summary>Atomically updates the local selection mirror after database validation or activation.</summary>
    /// <param name="keyId">The database-selected key identity.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    private async Task PersistActiveKeyAsync(string keyId, CancellationToken cancellationToken)
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
