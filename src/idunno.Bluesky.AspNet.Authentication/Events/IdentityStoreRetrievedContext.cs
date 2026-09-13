// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Collections.ObjectModel;

namespace idunno.Bluesky.AspNet.Authentication.Events;

/// <summary>
/// Context object passed to the <see cref="IdentityStoreEvents.PostRetrieval(IdentityStoreRetrievedContext)"/> method.
/// </summary>
/// <param name="identity">The byte representation of the identity that has been retrieved.</param>
/// <remarks>
/// <para>Creates a new instance of <see cref="IdentityStoreRetrievedContext"/>.</para>
/// </remarks>
public class IdentityStoreRetrievedContext(ReadOnlyMemory<byte> identity)
{
    /// <summary>
    /// Gets the byte representation of the identity that has been retrieved from the identity store.
    /// </summary>
    public ReadOnlyMemory<byte> Identity { get; private set; } = identity;

    /// <summary>
    /// Replaces the byte representation of the identity that has been retrieved from the identity store.
    /// </summary>
    /// <param name="identity">The new byte representation of the identity.</param>
    public void ReplaceIdentity(ReadOnlyMemory<byte> identity) => Identity = identity;

    /// <summary>
    /// Replaces the byte representation of the identity that has been retrieved from the identity store.
    /// </summary>
    /// <param name="identity">The new byte representation of the identity.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="identity"/> is <see langword="null" />.</exception>
    public void ReplaceIdentity(Collection<byte> identity)
    {
        ArgumentNullException.ThrowIfNull(identity);

        Identity = new ReadOnlyMemory<byte>([.. identity]);
    }
}