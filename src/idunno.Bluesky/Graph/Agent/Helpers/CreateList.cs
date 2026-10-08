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
    /// Creates a <see cref="List"/>.
    /// </summary>
    /// <param name="list">The <see cref="List"/> to create.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="list"/> is <see langword="null"/>.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<CreateRecordResult>> CreateList(
        List list,
        CancellationToken cancellationToken = default)
    {
        return await CreateList(rKey: null, list, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a <see cref="List"/> using the specified <paramref name="rKey"/>.
    /// </summary>
    /// <param name="rKey">The record key to use for the list record.</param>
    /// <param name="list">The <see cref="List"/> to create.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="list"/> is <see langword="null"/>.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to CreateRecord().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to CreateRecord().")]
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "The caller-supplied-key overload preserves the existing signature and cancellation-token call patterns.")]
    public async Task<AtProtoHttpResult<CreateRecordResult>> CreateList(
        RecordKey? rKey,
        List list,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(list);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await CreateRecord<BlueskyTimestampedRecord>(
            record: list,
            jsonSerializerOptions: BlueskyServer.BlueskyJsonSerializerOptions,
            collection: CollectionNsid.List,
            rKey: rKey,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
