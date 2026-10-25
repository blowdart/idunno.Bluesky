// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Labeler.Signing;

/// <summary>Persists a signing identity without storing private key material.</summary>
/// <remarks><para>This trusted hosting contract is not an administrative endpoint. Selection changes require
/// an existing authorized rotation with verified publication, completed re-signing and drained hosts.</para></remarks>
public interface ILabelSigningKeySelectionStore
{
    /// <summary>Reads the selection bound to this database's issuer and stream.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The selected identity, or <see langword="null"/> before initial setup.</returns>
    Task<LabelSigningKeySelection?> GetSigningKeySelectionAsync(CancellationToken cancellationToken = default);

    /// <summary>Seeds an absent selection without overwriting an existing identity.</summary>
    /// <param name="keyId">The explicitly configured initial key identity.</param>
    /// <param name="publicKey">The initial public multibase key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The authoritative selection, which may differ from the requested initial identity.</returns>
    /// <exception cref="ArgumentException">A key identity or public key is invalid.</exception>
    /// <exception cref="InvalidOperationException">The initial selection conflicts with persisted rotation state.</exception>
    Task<LabelSigningKeySelection> InitializeSigningKeySelectionAsync(
        string keyId,
        string publicKey,
        CancellationToken cancellationToken = default);

    /// <summary>Persists a verified rotation's replacement identity before local activation is acknowledged.</summary>
    /// <param name="operationId">The expected rotation identity.</param>
    /// <param name="previousKeyId">The expected previous key identity.</param>
    /// <param name="keyId">The replacement key identity.</param>
    /// <param name="publicKey">The replacement public multibase key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The authoritative replacement selection.</returns>
    /// <exception cref="ArgumentException">An operation identity, key identity or public key is invalid.</exception>
    /// <exception cref="InvalidOperationException">The rotation or selection does not permit activation.</exception>
    Task<LabelSigningKeySelection> ActivateSigningKeySelectionAsync(
        string operationId,
        string previousKeyId,
        string keyId,
        string publicKey,
        CancellationToken cancellationToken = default);
}
