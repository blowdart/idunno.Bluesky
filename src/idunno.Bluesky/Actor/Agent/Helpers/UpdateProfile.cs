// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Actor;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Update the current user's <see cref="Profile"/>.
    /// </summary>
    /// <param name="profile">The <see cref="AtProtoRepositoryRecord{Profile}"/> to update.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when the <paramref name="profile"/> is not valid.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Put().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Put().")]
    public async Task<AtProtoHttpResult<PutRecordResult>> UpdateProfile(
        AtProtoRepositoryRecord<Profile> profile,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(profile.Value);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        if (profile.Uri.Authority is not Did recordDid)
        {
            throw new ArgumentException("Uri authority is not a DID", nameof(profile));
        }

        if (recordDid != Did)
        {
            throw new ArgumentException("Uri authority does not match the current user", nameof(profile));
        }

        return await PutRecord(
            record: profile.Value,
            jsonSerializerOptions: BlueskyServer.BlueskyJsonSerializerOptions,
            collection: CollectionNsid.Profile,
            rKey: "self",
            validate: null,
            swapCommit: null,
            swapRecord: profile.Cid,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Update the current user's <see cref="Profile"/>.
    /// </summary>
    /// <param name="profile">The <see cref="AtProtoRepositoryRecord{Profile}"/> to update.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    public async Task<AtProtoHttpResult<PutRecordResult>> UpdateProfile(
        AtProtoRepositoryRecord<Profile> profile)
    {
        return await UpdateProfile(profile, default).ConfigureAwait(false);
    }

    /// <summary>
    /// Update the current user's <see cref="Profile"/>.
    /// </summary>
    /// <param name="profile">The <see cref="Profile"/> to update with.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="profile"/> is <see langword="null"/>.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Put().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Put().")]
    public async Task<AtProtoHttpResult<PutRecordResult>> UpdateProfile(
        Profile profile,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await PutRecord(
            record: profile,
            jsonSerializerOptions: BlueskyServer.BlueskyJsonSerializerOptions,
            collection: CollectionNsid.Profile,
            rKey: "self",
            validate: null,
            swapCommit: null,
            swapRecord: null,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Update the current user's <see cref="Profile"/>.
    /// </summary>
    /// <param name="profile">The <see cref="Profile"/> to update with.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="profile"/> is <see langword="null"/>.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Put().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Put().")]
    public async Task<AtProtoHttpResult<PutRecordResult>> UpdateProfile(
        Profile profile)
    {
        return await UpdateProfile(profile, default).ConfigureAwait(false);
    }
}
