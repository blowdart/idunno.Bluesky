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
public class IdentityStoreRetrievedContext(byte[] identity)
{
    /// <summary>
    /// Gets the byte representation of the identity that has been retrieved from the identity store.
    /// </summary>
    public ReadOnlyMemory<byte> Identity { get; private set; } = new ReadOnlyMemory<byte>(identity);

    /// <summary>
    /// Replaces the byte representation of the identity that has been retrieved from the identity store.
    /// </summary>
    /// <param name="identity">The new byte representation of the identity.</param>
    public void ReplaceIdentity(byte[] identity) => Identity = new ReadOnlyMemory<byte>(identity);

    /// <summary>
    /// Replaces the byte representation of the identity that has been retrieved from the identity store.
    /// </summary>
    /// <param name="identity">The new byte representation of the identity.</param>
    public void ReplaceIdentity(Collection<byte> identity) => Identity = new ReadOnlyMemory<byte>([.. identity]);
}