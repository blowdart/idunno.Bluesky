// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Graph;
using idunno.Bluesky.Record;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Creates a block record in the authenticated user's repo for the specified <paramref name="did"/>.
    /// </summary>
    /// <param name="did">The <see cref="Did"/> of the actor to block.</param>
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
    public async Task<AtProtoHttpResult<CreateRecordResult>> Block(Did did, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(did);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        Block block = new(did);

        // We use the BlueskyTimestampedRecordValue class as the generic so the type discriminator appears in the serialized output.
        return await CreateRecord<BlueskyTimestampedRecord>(
            record: block,
            jsonSerializerOptions: BlueskyServer.BlueskyJsonSerializerOptions,
            collection: CollectionNsid.Block,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
