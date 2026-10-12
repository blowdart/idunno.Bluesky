// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.Authentication;
using System.Security.Claims;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.AspNet.Authentication;
using idunno.Bluesky.AspNet.Authentication.Events;

namespace Samples.BlazorAuthentication;

/// <summary>
/// Coordinates identity mutations in this single-process sample so stale credential updates cannot restore ended sessions.
/// </summary>
/// <param name="inner">The underlying identity store.</param>
/// <remarks>
/// <para>Multi-process deployments require equivalent atomic session checks in their durable store, not this process-local gate.</para>
/// </remarks>
public sealed class SessionIdentityStore(IIdentityStore inner) : IIdentityStore, IDisposable
{
    private readonly SemaphoreSlim _mutations = new(1, 1);

    /// <inheritdoc/>
    public IdentityStoreEvents Events
    {
        get => inner.Events;
        set => inner.Events = value;
    }

    /// <inheritdoc/>
    public async Task Add(ClaimsIdentity claimsIdentity, CancellationToken cancellationToken = default)
    {
        await _mutations.WaitAsync(cancellationToken);
        try
        {
            await inner.Add(claimsIdentity, cancellationToken);
        }
        finally
        {
            _mutations.Release();
        }
    }

    /// <inheritdoc/>
    public Task<ClaimsIdentity?> GetIdentity(Did did, CancellationToken cancellationToken = default) =>
        inner.GetIdentity(did, cancellationToken);

    /// <inheritdoc/>
    public async Task Remove(Did did, CancellationToken cancellationToken = default)
    {
        await _mutations.WaitAsync(cancellationToken);
        try
        {
            await inner.Remove(did, cancellationToken);
        }
        finally
        {
            _mutations.Release();
        }
    }

    /// <inheritdoc/>
    public async Task Update(ClaimsIdentity identity, CancellationToken cancellationToken)
    {
        await _mutations.WaitAsync(cancellationToken);
        try
        {
            await RequireCurrentAsync(identity, cancellationToken);
            await inner.Update(identity, cancellationToken);
        }
        finally
        {
            _mutations.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<bool> UpdateIfNewer(AccessCredentials credentials, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        var identity = IIdentityStore.BuildClaimsIdentity(credentials);
        await _mutations.WaitAsync(cancellationToken);
        try
        {
            // The proof check and write share the gate with login replacement and logout removal.
            await RequireCurrentAsync(identity, cancellationToken);
            return await inner.UpdateIfNewer(credentials, cancellationToken);
        }
        finally
        {
            _mutations.Release();
        }
    }

    private async Task RequireCurrentAsync(ClaimsIdentity identity, CancellationToken cancellationToken)
    {
        var did = new Did(identity.FindFirst(AtProtoClaims.Did)!.Value);
        var current = await inner.GetIdentity(did, cancellationToken);
        string? proof = identity.FindFirst(AtProtoClaims.DPoPProof)?.Value;
        if (proof is null || current?.FindFirst(AtProtoClaims.DPoPProof)?.Value != proof)
        {
            throw new AuthenticationException("Your session has ended. Sign in again.");
        }
    }

    /// <inheritdoc/>
    public Task<string?> StartRefresh(Did did, CancellationToken cancellationToken = default) =>
        inner.StartRefresh(did, cancellationToken);

    /// <inheritdoc/>
    public Task<bool> EndRefresh(Did did, string? refreshLockToken, CancellationToken cancellationToken = default) =>
        inner.EndRefresh(did, refreshLockToken, cancellationToken);

    /// <inheritdoc/>
    public Task<bool> IsRefreshing(Did did, CancellationToken cancellationToken = default) =>
        inner.IsRefreshing(did, cancellationToken);

    /// <summary>
    /// Releases the sample's mutation gate.
    /// </summary>
    public void Dispose() => _mutations.Dispose();
}
