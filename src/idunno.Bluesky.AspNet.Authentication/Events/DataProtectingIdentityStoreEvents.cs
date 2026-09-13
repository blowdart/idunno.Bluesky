// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.DataProtection;

namespace idunno.Bluesky.AspNet.Authentication.Events;

/// <summary>
/// An <see cref="IdentityStoreEvents"/> implementation that encrypts identities before they are stored and
/// decrypts them after they are retrieved, using an <see cref="IDataProtector"/>.
/// </summary>
/// <remarks>
/// <para>
///   Only the serialized identity payload is protected. Any keys derived from the identity, such as the DID used
///   as a cache key, and any refresh lock entries are not affected by this class.
/// </para>
/// <para>
///   The <see cref="IDataProtector"/> key ring must be shared and persisted across every instance that reads from
///   the identity store, otherwise instances will be unable to decrypt each other's entries.
/// </para>
/// </remarks>
public class DataProtectingIdentityStoreEvents : IdentityStoreEvents
{
    /// <summary>
    /// The purpose string used when creating a protector from an <see cref="IDataProtectionProvider"/>.
    /// </summary>
    public const string ProtectorPurpose = "idunno.Bluesky.AspNet.Authentication.IdentityStore.v1";

    private readonly IDataProtector _protector;

    /// <summary>
    /// Creates a new instance of <see cref="DataProtectingIdentityStoreEvents"/> using the supplied <see cref="IDataProtector"/>.
    /// </summary>
    /// <param name="protector">The <see cref="IDataProtector"/> used to protect and unprotect stored identities.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="protector"/> is <see langword="null" />.</exception>
    public DataProtectingIdentityStoreEvents(IDataProtector protector)
    {
        ArgumentNullException.ThrowIfNull(protector);

        _protector = protector;
    }

    /// <summary>
    /// Creates a new instance of <see cref="DataProtectingIdentityStoreEvents"/> using a protector created from the
    /// supplied <see cref="IDataProtectionProvider"/> and the <see cref="ProtectorPurpose"/>.
    /// </summary>
    /// <param name="dataProtectionProvider">The <see cref="IDataProtectionProvider"/> used to create the protector.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="dataProtectionProvider"/> is <see langword="null" />.</exception>
    public DataProtectingIdentityStoreEvents(IDataProtectionProvider dataProtectionProvider)
    {
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);

        _protector = dataProtectionProvider.CreateProtector(ProtectorPurpose);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is <see langword="null" />.</exception>
    public override Task PreStoring(IdentityStoreSettingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        byte[] protectedIdentity = _protector.Protect([.. context.Identity]);
        context.ReplaceIdentity(protectedIdentity);

        return base.PreStoring(context);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is <see langword="null" />.</exception>
    public override Task PostRetrieval(IdentityStoreRetrievedContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        byte[] unprotectedIdentity = _protector.Unprotect(context.Identity.ToArray());
        context.ReplaceIdentity(unprotectedIdentity);

        return base.PostRetrieval(context);
    }
}
