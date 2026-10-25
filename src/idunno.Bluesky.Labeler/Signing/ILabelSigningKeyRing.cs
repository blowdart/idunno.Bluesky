// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Labeler.Signing;

/// <summary>Resolves signing keys installed in a host-controlled secret store and atomically activates one key identity.</summary>
/// <remarks>
/// <para>The provider owns every returned <see cref="idunno.AtProto.Labels.LabelSigningKey"/>; callers must not dispose it.
/// Remote administration sends key identities and public values only, never private key material.</para>
/// <para>Implementations must persist active-key selection so a restart preserves the selected signing identity.</para>
/// </remarks>
public interface ILabelSigningKeyRing
{
    /// <summary>Gets the identity of the currently active signing key.</summary>
    string ActiveKeyId { get; }

    /// <summary>Gets a host-owned signing key by its configured identity.</summary>
    /// <param name="keyId">The configured key identity.</param>
    /// <returns>The host-owned signing key.</returns>
    /// <exception cref="KeyNotFoundException">The key identity is not configured.</exception>
    idunno.AtProto.Labels.LabelSigningKey GetSigningKey(string keyId);

    /// <summary>Gets the public multibase value for a configured signing key.</summary>
    /// <param name="keyId">The configured key identity.</param>
    /// <returns>The public key value.</returns>
    /// <exception cref="KeyNotFoundException">The key identity is not configured.</exception>
    string GetPublicKeyMultibase(string keyId);

    /// <summary>Persists the active key identity without exporting or transferring private key material.</summary>
    /// <param name="keyId">The configured key identity to activate.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the activation.</returns>
    /// <exception cref="KeyNotFoundException">The key identity is not configured.</exception>
    Task ActivateAsync(string keyId, CancellationToken cancellationToken = default);
}
