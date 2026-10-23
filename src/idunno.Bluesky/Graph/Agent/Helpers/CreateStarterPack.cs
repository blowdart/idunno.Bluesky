// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Graph;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Creates a starter pack in the current user's repository.
    /// </summary>
    /// <param name="starterPack">The starter pack to create.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="starterPack"/> is <see langword="null"/>.</exception>
    /// <exception cref="AuthenticationRequiredException">The current agent is not authenticated.</exception>
    /// <remarks>
    /// <para>The starter pack's list must already exist. This method creates only the starter pack record.</para>
    /// </remarks>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "The required leading record key distinguishes the keyed overload from this record-only overload.")]
    public async Task<AtProtoHttpResult<CreateRecordResult>> CreateStarterPack(
        StarterPack starterPack,
        CancellationToken cancellationToken = default)
    {
        return await CreateStarterPack(rKey: null, starterPack, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a starter pack using the specified record key in the current user's repository.
    /// </summary>
    /// <param name="rKey">An optional record key to use for the starter pack record.</param>
    /// <param name="starterPack">The starter pack to create.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="starterPack"/> is <see langword="null"/>.</exception>
    /// <exception cref="AuthenticationRequiredException">The current agent is not authenticated.</exception>
    /// <remarks>
    /// <para>The starter pack's list must already exist. This method creates only the starter pack record.</para>
    /// </remarks>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "The required leading record key distinguishes this overload from the record-only overload.")]
    public async Task<AtProtoHttpResult<CreateRecordResult>> CreateStarterPack(
        RecordKey? rKey,
        StarterPack starterPack,
        CancellationToken cancellationToken = default)
    {
        return await CreateBlueskyRecord(
            record: starterPack,
            collection: CollectionNsid.StarterPack,
            rKey: rKey,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
