// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Collections.ObjectModel;

namespace idunno.AtProto.Authentication;

/// <summary>
/// Keeps nonce updates scoped to the origin which supplied them, independently of token rotation.
/// </summary>
internal sealed class DPoPNonceStore
{
    private readonly ConcurrentDictionary<string, string> _nonces = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets a stable snapshot suitable for credential persistence.
    /// </summary>
    internal IReadOnlyDictionary<string, string> Snapshot => new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(_nonces));

    /// <summary>
    /// Gets the nonce for a destination, without borrowing another origin's nonce.
    /// </summary>
    internal string? Get(Uri destination) => _nonces.TryGetValue(GetOrigin(destination), out string? nonce) ? nonce : null;

    /// <summary>
    /// Clears a nonce explicitly reset by a credential owner.
    /// </summary>
    internal void Remove(Uri destination) => _nonces.TryRemove(GetOrigin(destination), out _);

    /// <summary>
    /// Gets the normalized scheme, host and port, excluding any user information or request path.
    /// </summary>
    internal static string GetOrigin(Uri destination) => destination.GetComponents(UriComponents.SchemeAndServer, UriFormat.UriEscaped);

    /// <summary>
    /// Records a nonempty nonce from a response and reports whether it changed.
    /// </summary>
    internal bool Set(Uri destination, string? nonce)
    {
        if (string.IsNullOrWhiteSpace(nonce))
        {
            return false;
        }

        string origin = GetOrigin(destination);
        bool changed = !_nonces.TryGetValue(origin, out string? previous) || !string.Equals(previous, nonce, StringComparison.Ordinal);
        _nonces[origin] = nonce;

        return changed;
    }

    /// <summary>
    /// Restores a saved snapshot, validating and normalizing its origin keys.
    /// </summary>
    internal void Restore(IReadOnlyDictionary<string, string> nonces)
    {
        ArgumentNullException.ThrowIfNull(nonces);

        foreach (KeyValuePair<string, string> entry in nonces)
        {
            if (!Uri.TryCreate(entry.Key, UriKind.Absolute, out Uri? origin) ||
                origin.Scheme is not ("http" or "https") ||
                !string.Equals(entry.Key.TrimEnd('/'), GetOrigin(origin), StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(entry.Value))
            {
                throw new ArgumentException("Nonce entries must contain an HTTP origin and a nonempty nonce.", nameof(nonces));
            }

            Set(origin, entry.Value);
        }
    }
}

/// <summary>
/// Shares origin-bound nonce state between credentials for the same proof key.
/// </summary>
internal interface IOriginBoundDPoPCredential
{
    /// <summary>
    /// Gets or sets the nonce state retained across token rotation.
    /// </summary>
    DPoPNonceStore NonceStore { get; set; }
}
