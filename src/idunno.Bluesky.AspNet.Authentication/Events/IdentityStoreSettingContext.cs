// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Collections.ObjectModel;

namespace idunno.Bluesky.AspNet.Authentication.Events;

/// <summary>
/// Context object passed to the <see cref="IdentityStoreEvents.PreStoring(IdentityStoreSettingContext)"/> method.
/// </summary>
/// <param name="identity">The byte representation of the identity to be stored.</param>
/// <remarks>
/// <para>Creates a new instance of <see cref="IdentityStoreSettingContext"/>.</para>
/// </remarks>
public class IdentityStoreSettingContext(byte[] identity)
{
    /// <summary>
    /// Gets the byte representation of the identity to be stored.
    /// </summary>
    public Collection<byte> Identity { get; private set; } = new Collection<byte>(identity);

    /// <summary>
    /// Replaces the byte representation of the identity to be stored.
    /// </summary>
    /// <param name="identity">The new byte representation of the identity.</param>
    public void ReplaceIdentity(byte[] identity) => Identity = new Collection<byte>(identity);

    /// <summary>
    /// Replaces the byte representation of the identity to be stored.
    /// </summary>
    /// <param name="identity">The new byte representation of the identity.</param>
    public void ReplaceIdentity(Collection<byte> identity) => Identity = identity;
}
