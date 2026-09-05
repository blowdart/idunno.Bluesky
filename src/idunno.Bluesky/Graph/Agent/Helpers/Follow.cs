// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Net;

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Graph;
using idunno.Bluesky.Record;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Creates a follow record in the authenticated user's repo for the specified <paramref name="handle"/>.
    /// </summary>
    /// <param name="handle">The <see cref="Handle"/> of the actor to follow.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handle"/> is <see langword="null"/>.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<CreateRecordResult>> Follow(Handle handle, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handle);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        Did? didResolutionResult = await ResolveHandle(handle, cancellationToken).ConfigureAwait(false);

        if (didResolutionResult is null)
        {
            Logger.FollowFailedAsHandleCouldNotResolve(_logger, handle);
        }

        if (didResolutionResult is null || cancellationToken.IsCancellationRequested)
        {
            return new AtProtoHttpResult<CreateRecordResult>(
                null,
                HttpStatusCode.NotFound,
                null,
                null);
        }

        return await Follow(didResolutionResult, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a follow record in the authenticated user's repo for the specified <paramref name="did"/>.
    /// </summary>
    /// <param name="did">The <see cref="Did"/> of the actor to follow.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="did"/> is <see langword="null"/>.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to CreateRecord().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to CreateRecord().")]
    public async Task<AtProtoHttpResult<CreateRecordResult>> Follow(Did did, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(did);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        Follow follow = new(did);

        // We use the BlueskyTimestampedRecordValue class as the generic so the type discriminator appears in the serialized output.
        AtProtoHttpResult<CreateRecordResult> result = await CreateRecord<BlueskyTimestampedRecord>(
            record: follow,
            jsonSerializerOptions: BlueskyServer.BlueskyJsonSerializerOptions,
            collection: CollectionNsid.Follow,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (result.Succeeded)
        {
            Logger.FollowSucceeded(_logger, Did, follow.Subject);
        }
        else
        {
            Logger.FollowFailedAtApiLayer(_logger, Did, follow.Subject);
        }

        return result;
    }
}