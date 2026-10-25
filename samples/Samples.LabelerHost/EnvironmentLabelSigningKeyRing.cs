// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.Cryptography;
using System.Text;

using idunno.AtProto.Labels;
using idunno.Bluesky.Labeler;
using idunno.Bluesky.Labeler.Maintenance;
using idunno.Bluesky.Labeler.Signing;

namespace Samples.LabelerHost;

/// <summary>Loads separately configured private keys into memory and persists only their public selection.</summary>
internal sealed class EnvironmentLabelSigningKeyRing : ILabelSigningKeyRing, IDisposable
{
    private readonly Dictionary<string, LabelSigningKey> _keys = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ILabelSigningKeySelectionStore? _selectionStore;
    private ILabelMaintenanceStore? _maintenance;
    private string? _activeKeyId;
    private int _disposed;

    /// <summary>Initializes a new instance of the <see cref="EnvironmentLabelSigningKeyRing"/> class.</summary>
    /// <param name="configuration">The host configuration containing separate protected key values.</param>
    internal EnvironmentLabelSigningKeyRing(IConfiguration configuration)
    {
        try
        {
            foreach (IConfigurationSection entry in configuration.GetSection("Labeler:SigningKeys").GetChildren())
            {
                string? keyId = entry["Id"];
                if (string.IsNullOrWhiteSpace(keyId) || keyId.Length > 128 ||
                    keyId.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.')) ||
                    _keys.ContainsKey(keyId))
                {
                    throw new InvalidOperationException("Signing key identities must be valid and unique.");
                }

                string value = entry["PrivateKeyBase64"]
                    ?? throw new InvalidOperationException("Each signing key requires a protected base64 PEM value.");
                byte[] pem;
                try
                {
                    pem = Convert.FromBase64String(value);
                }
                catch (FormatException)
                {
                    throw new InvalidOperationException("A configured signing key is not base64-encoded PEM.");
                }

                char[] characters = Encoding.UTF8.GetChars(pem);
                try
                {
                    _keys.Add(keyId, LabelSigningKey.ImportFromPem(characters));
                }
                catch (Exception exception) when (exception is ArgumentException or CryptographicException)
                {
                    throw new InvalidOperationException("A configured signing key is not a supported private PEM key.");
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(pem);
                    Array.Clear(characters);
                }
            }

            if (_keys.Count == 0)
            {
                throw new InvalidOperationException("Configure at least one protected signing key.");
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <summary>Loads or seeds the durable selection before the host can serve traffic.</summary>
    /// <param name="selectionStore">The initialized label database's selection provider.</param>
    /// <param name="maintenance">The same database's maintenance provider.</param>
    /// <param name="initialKeyId">The explicit initial selection, used only when no selection exists.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    internal async Task InitializeAsync(
        ILabelSigningKeySelectionStore selectionStore,
        ILabelMaintenanceStore maintenance,
        string? initialKeyId,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_selectionStore is not null)
            {
                throw new InvalidOperationException("The signing key ring is already initialized.");
            }

            LabelSigningKeySelection? selection = await selectionStore.GetSigningKeySelectionAsync(cancellationToken).ConfigureAwait(false);
            if (selection is null)
            {
                if (string.IsNullOrWhiteSpace(initialKeyId))
                {
                    throw new InvalidOperationException("Configure Labeler:InitialSigningKeyId for first setup.");
                }

                selection = await selectionStore.InitializeSigningKeySelectionAsync(
                    initialKeyId,
                    GetPublicKeyMultibase(initialKeyId),
                    cancellationToken).ConfigureAwait(false);
            }

            if (GetPublicKeyMultibase(selection.KeyId) != selection.PublicKey)
            {
                throw new InvalidOperationException("The configured private key does not match the persisted selection.");
            }

            _maintenance = maintenance;
            _selectionStore = selectionStore;
            Volatile.Write(ref _activeKeyId, selection.KeyId);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc/>
    public string ActiveKeyId
    {
        get
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            return Volatile.Read(ref _activeKeyId)
                ?? throw new InvalidOperationException("Initialize the signing key ring before use.");
        }
    }

    /// <inheritdoc/>
    public LabelSigningKey GetSigningKey(string keyId)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        return _keys.TryGetValue(keyId, out LabelSigningKey? key)
            ? key
            : throw new KeyNotFoundException("The selected signing key is not configured on this host.");
    }

    /// <inheritdoc/>
    public string GetPublicKeyMultibase(string keyId) => GetSigningKey(keyId).ExportPublicKeyMultibase();

    /// <inheritdoc/>
    public async Task ActivateAsync(string keyId, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
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
            Volatile.Write(ref _activeKeyId, selected.KeyId);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Disposes every private key loaded by this provider.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            foreach (LabelSigningKey key in _keys.Values)
            {
                key.Dispose();
            }

            _gate.Dispose();
        }
    }
}
