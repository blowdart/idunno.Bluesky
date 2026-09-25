// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.DataProtection;

namespace idunno.Bluesky.AspNet.Authentication.Events;

/// <summary>
/// A <see cref="CorrelationStateCacheEvents"/> implementation that encrypts correlation state before it is stored and
/// decrypts it after it is retrieved, using an <see cref="IDataProtector"/>.
/// </summary>
/// <remarks>
/// <para>
///   Correlation state holds the PKCE code verifier and, for a DPoP login, the DPoP private key for an in flight login,
///   so a cache an attacker can read is enough to complete someone else's login. Protecting it matters even though the
///   entries are short lived.
/// </para>
/// <para>
///   Only the serialized state is protected. The correlation identifier used as the cache key is not.
/// </para>
/// <para>
///   The <see cref="IDataProtector"/> key ring must be shared and persisted across every instance that reads from the
///   correlation state cache, otherwise a login started on one instance cannot be completed on another.
/// </para>
/// </remarks>
public class DataProtectingCorrelationStateCacheEvents : CorrelationStateCacheEvents
{
    /// <summary>
    /// The purpose string used when creating a protector from an <see cref="IDataProtectionProvider"/>.
    /// </summary>
    public const string ProtectorPurpose = "idunno.Bluesky.AspNet.Authentication.CorrelationStateCache.v1";

    private readonly IDataProtector _protector;

    /// <summary>
    /// Creates a new instance of <see cref="DataProtectingCorrelationStateCacheEvents"/> using the supplied <see cref="IDataProtector"/>.
    /// </summary>
    /// <param name="protector">The <see cref="IDataProtector"/> used to protect and unprotect stored correlation state.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="protector"/> is <see langword="null" />.</exception>
    public DataProtectingCorrelationStateCacheEvents(IDataProtector protector)
    {
        ArgumentNullException.ThrowIfNull(protector);

        _protector = protector;
    }

    /// <summary>
    /// Creates a new instance of <see cref="DataProtectingCorrelationStateCacheEvents"/> using a protector created from the
    /// supplied <see cref="IDataProtectionProvider"/> and the <see cref="ProtectorPurpose"/>.
    /// </summary>
    /// <param name="dataProtectionProvider">The <see cref="IDataProtectionProvider"/> used to create the protector.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="dataProtectionProvider"/> is <see langword="null" />.</exception>
    public DataProtectingCorrelationStateCacheEvents(IDataProtectionProvider dataProtectionProvider)
    {
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);

        _protector = dataProtectionProvider.CreateProtector(ProtectorPurpose);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is <see langword="null" />.</exception>
    /// <remarks>
    /// <para>
    ///   Any <see cref="CorrelationStateCacheEvents.OnStoring"/> delegate runs before the state is protected, so it sees, and
    ///   can replace, the unprotected state. Protection is always the outermost layer.
    /// </para>
    /// </remarks>
    public override async Task PreStoring(CorrelationStateSettingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        await base.PreStoring(context).ConfigureAwait(false);

        context.ReplaceState(_protector.Protect(context.State));
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is <see langword="null" />.</exception>
    /// <remarks>
    /// <para>
    ///   The state is unprotected before any <see cref="CorrelationStateCacheEvents.OnRetrieved"/> delegate runs, so the
    ///   delegate sees the unprotected state, mirroring <see cref="PreStoring(CorrelationStateSettingContext)"/>.
    /// </para>
    /// </remarks>
    public override async Task PostRetrieval(CorrelationStateRetrievedContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.ReplaceState(_protector.Unprotect(context.State));

        await base.PostRetrieval(context).ConfigureAwait(false);
    }
}
