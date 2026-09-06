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
    /// Updates the status record for the authenticated user
    /// </summary>
    /// <param name="status">The status record to update. The record's URI authority must match the current user's decentralized identifier (DID).</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous update operation. The task result contains the outcome of the
    /// update, including the updated record information.</returns>
    /// <exception cref="AuthenticationRequiredException">Thrown if the user is not authenticated when attempting to update the status.</exception>
    /// <exception cref="ArgumentException">Thrown if the status record's URI authority is not a valid DID or does not match the current user's DID.</exception>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    public async Task<AtProtoHttpResult<PutRecordResult>> UpdateStatus(
        AtProtoRepositoryRecord<Status> status,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(status);
        ArgumentNullException.ThrowIfNull(status.Value);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }
        if (status.Uri.Authority is not Did recordDid)
        {
            throw new ArgumentException("Uri authority is not a DID", nameof(status));
        }
        if (recordDid != Did)
        {
            throw new ArgumentException("Uri authority does not match the current user", nameof(status));
        }

        return await PutRecord(
            record: status.Value,
            jsonSerializerOptions: BlueskyServer.BlueskyJsonSerializerOptions,
            collection: CollectionNsid.Status,
            rKey: "self",
            validate: null,
            swapCommit: null,
            swapRecord: status.Cid,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
