// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Memory;

namespace Samples.AspNetProgressiveAuthentication;

/// <summary>
/// Captures the validated editor values and the original profile version for a later conditional save.
/// </summary>
/// <param name="DisplayName">The edited display name.</param>
/// <param name="Description">The edited description.</param>
/// <param name="Pronouns">The edited pronouns.</param>
/// <param name="Cid">The original profile CID used to prevent overwriting intervening changes.</param>
internal sealed record ProfileEdit(string DisplayName, string Description, string Pronouns, string Cid);

/// <summary>
/// Binds a draft to both an account and one browser's protected authentication ticket.
/// </summary>
/// <param name="Did">The authenticated account DID.</param>
/// <param name="Session">The random editing-session identifier stored in the protected ticket.</param>
internal sealed record ProfileEditOwner(string Did, string Session);

/// <summary>
/// Describes whether a draft is awaiting consent, exchanging it, ready for one save, or retained for recovery.
/// </summary>
internal enum ProfileEditStatus { AwaitingConsent, ProcessingConsent, Ready, Failed }

/// <summary>
/// Exposes a decrypted draft and its workflow state to the owner's editor.
/// </summary>
/// <param name="Id">The opaque operation identifier.</param>
/// <param name="Edit">The saved editor values.</param>
/// <param name="Status">The consent or completion state.</param>
/// <param name="Message">The recovery explanation, if any.</param>
internal sealed record PendingProfileEdit(string Id, ProfileEdit Edit, ProfileEditStatus Status, string? Message);

/// <summary>
/// Provides source-generated serialization metadata for the encrypted draft payload.
/// </summary>
[JsonSerializable(typeof(ProfileEdit))]
internal partial class ProfileEditJsonContext : JsonSerializerContext;

/// <summary>
/// Retains encrypted, expiring profile edits for this single-process sample.
/// </summary>
/// <remarks>
/// <para>
/// One draft is retained per account/session pair, for at most ten minutes. A new submission replaces the previous draft.
/// The cache holds encrypted editor values; only an owner-bound lookup decrypts them. Identifiers alone never authorize access.
/// </para>
/// <para>
/// The lock makes state checks and transitions atomic within this process, preventing repeated consent exchanges and saves.
/// Expiry, cache capacity eviction and application restart can discard drafts. Production deployments need shared durable
/// storage with equivalent encryption, expiry and atomic transitions; sharing a data protection key ring is not sufficient.
/// </para>
/// </remarks>
public sealed class ProfileEditStore : IDisposable
{
    private sealed record Entry(string Id, byte[] Payload, DateTimeOffset Expires, ProfileEditStatus Status, string? Message);

    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = 1000 });
    private readonly Lock _gate = new();
    private readonly IDataProtector _protector;
    private readonly TimeProvider _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileEditStore"/> class.
    /// </summary>
    /// <param name="protection">The data protection provider.</param>
    /// <param name="clock">The expiry clock.</param>
    public ProfileEditStore(IDataProtectionProvider protection, TimeProvider clock)
    {
        _protector = protection.CreateProtector("Samples.AspNetProgressiveAuthentication.ProfileEdit.v1");
        _clock = clock;
    }

    /// <summary>
    /// Encrypts a newly submitted edit and replaces any draft belonging to the same owner.
    /// </summary>
    /// <param name="owner">The authenticated account and editing session.</param>
    /// <param name="edit">The validated editor values and original profile CID.</param>
    /// <param name="status">The initial state, normally awaiting consent or retained during a direct save.</param>
    /// <param name="message">The initial recovery explanation, if any.</param>
    /// <returns>A fresh opaque identifier for this operation.</returns>
    internal string Add(ProfileEditOwner owner, ProfileEdit edit, ProfileEditStatus status = ProfileEditStatus.AwaitingConsent, string? message = null)
    {
        string id = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        Entry entry = new(id, _protector.Protect(JsonSerializer.SerializeToUtf8Bytes(edit, ProfileEditJsonContext.Default.ProfileEdit)),
            _clock.GetUtcNow().AddMinutes(10), status, message);
        lock (_gate)
        {
            Put(owner, entry);
        }

        return id;
    }

    /// <summary>
    /// Retrieves the owner's current unexpired draft without consuming consent or completion.
    /// </summary>
    /// <param name="owner">The authenticated account and editing session.</param>
    /// <returns>The decrypted draft, or <see langword="null"/> if it is unavailable.</returns>
    internal PendingProfileEdit? Get(ProfileEditOwner owner)
    {
        lock (_gate)
        {
            return Read(owner) is Entry entry ? Decode(entry) : null;
        }
    }

    /// <summary>
    /// Claims an awaiting draft for exactly one OAuth callback's token exchange.
    /// </summary>
    /// <param name="owner">The original authenticated account and editing session.</param>
    /// <param name="id">The draft identifier from correlated server-side OAuth state.</param>
    /// <returns><see langword="true"/> if the callback claimed the draft; otherwise, <see langword="false"/>.</returns>
    internal bool TakeConsent(ProfileEditOwner owner, string id)
    {
        lock (_gate)
        {
            if (Read(owner) is not Entry entry || entry.Id != id || entry.Status != ProfileEditStatus.AwaitingConsent)
            {
                return false;
            }

            Put(owner, entry with { Status = ProfileEditStatus.ProcessingConsent });
            return true;
        }
    }

    /// <summary>
    /// Marks a claimed draft ready for completion or retains it for recovery after failed consent.
    /// </summary>
    /// <param name="owner">The original authenticated account and editing session.</param>
    /// <param name="id">The claimed draft identifier.</param>
    /// <param name="authorized"><see langword="true"/> if validated credentials were installed; otherwise, <see langword="false"/>.</param>
    /// <param name="message">The explanation for a failed consent attempt, if any.</param>
    /// <returns><see langword="true"/> if the draft was still claimed and unexpired; otherwise, <see langword="false"/>.</returns>
    internal bool FinishConsent(ProfileEditOwner owner, string id, bool authorized, string? message = null)
    {
        lock (_gate)
        {
            if (Read(owner) is not Entry entry || entry.Id != id || entry.Status != ProfileEditStatus.ProcessingConsent)
            {
                return false;
            }

            Put(owner, entry with { Status = authorized ? ProfileEditStatus.Ready : ProfileEditStatus.Failed, Message = message });
            return true;
        }
    }

    /// <summary>
    /// Consumes a ready draft's authorization for exactly one conditional profile save.
    /// </summary>
    /// <param name="owner">The authenticated account and editing session.</param>
    /// <param name="id">The completion identifier submitted with the protected form.</param>
    /// <returns>The saved editor values, or <see langword="null"/> if completion is not authorized.</returns>
    /// <remarks>
    /// <para>
    /// The draft remains recoverable, but is no longer ready. A repeated completion cannot retry a write, even when
    /// the first write's outcome is uncertain. The caller removes the draft only after a confirmed successful save.
    /// </para>
    /// </remarks>
    internal ProfileEdit? TakeReady(ProfileEditOwner owner, string id)
    {
        lock (_gate)
        {
            if (Read(owner) is not Entry entry || entry.Id != id || entry.Status != ProfileEditStatus.Ready)
            {
                return null;
            }

            Put(owner, entry with
            {
                Status = ProfileEditStatus.Failed,
                Message = "The save was started but its outcome has not been confirmed. Check your profile before retrying."
            });
            return Decode(entry).Edit;
        }
    }

    /// <summary>
    /// Removes a completed or explicitly discarded draft only if its identifier still matches.
    /// </summary>
    /// <param name="owner">The authenticated account and editing session.</param>
    /// <param name="id">The draft identifier to remove, without affecting a newer submission.</param>
    internal void Remove(ProfileEditOwner owner, string id)
    {
        lock (_gate)
        {
            if (Read(owner)?.Id == id)
            {
                _cache.Remove(owner);
            }
        }
    }

    /// <summary>
    /// Invalidates the session's current draft when the session signs out or is replaced by a new login.
    /// </summary>
    /// <param name="owner">The account and editing session being ended.</param>
    internal void Invalidate(ProfileEditOwner owner)
    {
        lock (_gate)
        {
            _cache.Remove(owner);
        }
    }

    /// <summary>
    /// Reads an encrypted entry and rejects expired entries using the configured clock.
    /// </summary>
    /// <param name="owner">The account/session cache key.</param>
    /// <returns>The unexpired encrypted entry, or <see langword="null"/> if it is unavailable.</returns>
    /// <remarks>
    /// <para>Call while holding the store lock so expiry checks and subsequent transitions form one operation.</para>
    /// </remarks>
    private Entry? Read(ProfileEditOwner owner)
    {
        if (_cache.TryGetValue(owner, out Entry? entry) && entry is not null && entry.Expires > _clock.GetUtcNow())
        {
            return entry;
        }

        _cache.Remove(owner);
        return null;
    }

    /// <summary>
    /// Stores an encrypted entry without extending the original draft's expiry.
    /// </summary>
    /// <param name="owner">The account/session cache key.</param>
    /// <param name="entry">The encrypted entry with its original absolute expiry.</param>
    /// <remarks>
    /// <para>Call while holding the store lock. The relative cache lifetime follows the remaining time on the configured clock.</para>
    /// </remarks>
    private void Put(ProfileEditOwner owner, Entry entry) =>
        _cache.Set(owner, entry, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = entry.Expires - _clock.GetUtcNow(),
            Size = 1
        });

    /// <summary>
    /// Authenticates and decrypts a stored payload for an owner-authorized lookup.
    /// </summary>
    /// <param name="entry">The encrypted entry already selected by account and session.</param>
    /// <returns>The decrypted edit and its workflow state.</returns>
    /// <remarks>
    /// <para>Protection and serialization failures propagate rather than returning empty edits or a success-shaped fallback.</para>
    /// </remarks>
    private PendingProfileEdit Decode(Entry entry) =>
        new(entry.Id, JsonSerializer.Deserialize(_protector.Unprotect(entry.Payload), ProfileEditJsonContext.Default.ProfileEdit)
            ?? throw new InvalidOperationException("Missing saved profile edit."), entry.Status, entry.Message);

    /// <inheritdoc/>
    public void Dispose() => _cache.Dispose();
}
