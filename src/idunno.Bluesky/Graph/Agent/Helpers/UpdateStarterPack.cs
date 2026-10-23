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
    /// Updates the starter pack record referenced by <paramref name="uri"/> in the current user's repository.
    /// </summary>
    /// <param name="uri">The URI of the starter pack record to update.</param>
    /// <param name="starterPack">The replacement starter pack value.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="uri"/>, its collection or record key, or <paramref name="starterPack"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="uri"/> does not identify a starter pack owned by the current user.</exception>
    /// <exception cref="AuthenticationRequiredException">The current agent is not authenticated.</exception>
    /// <remarks>
    /// <para>This method replaces the starter pack record without comparing its previous CID. It does not modify the referenced list or feeds.</para>
    /// </remarks>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "The repository-record and URI overloads have distinct required parameters.")]
    public async Task<AtProtoHttpResult<PutRecordResult>> UpdateStarterPack(
        AtUri uri,
        StarterPack starterPack,
        CancellationToken cancellationToken = default)
    {
        return await UpdateStarterPack(uri, starterPack, swapRecord: null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Updates a starter pack record conditionally using its previous CID in the current user's repository.
    /// </summary>
    /// <param name="starterPack">The repository record containing the replacement starter pack value and the CID to replace.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="starterPack"/>, its value, or its URI's collection or record key is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The record URI does not identify a starter pack owned by the current user.</exception>
    /// <exception cref="AuthenticationRequiredException">The current agent is not authenticated.</exception>
    /// <remarks>
    /// <para>The record's CID is sent as <c>swapRecord</c> to prevent overwriting a concurrent change. The referenced list and feeds are not modified.</para>
    /// </remarks>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "The repository-record and URI overloads have distinct required parameters.")]
    public async Task<AtProtoHttpResult<PutRecordResult>> UpdateStarterPack(
        AtProtoRepositoryRecord<StarterPack> starterPack,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(starterPack);

        return await UpdateStarterPack(starterPack.Uri, starterPack.Value, starterPack.Cid, cancellationToken).ConfigureAwait(false);
    }

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "StarterPack is registered in the source-generated Bluesky JSON serializer context.")]
    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "StarterPack is registered in the source-generated Bluesky JSON serializer context.")]
    private async Task<AtProtoHttpResult<PutRecordResult>> UpdateStarterPack(
        AtUri uri,
        StarterPack starterPack,
        Cid? swapRecord,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(starterPack);
        ArgumentNullException.ThrowIfNull(uri.Collection);
        ArgumentNullException.ThrowIfNull(uri.RecordKey);
        ArgumentOutOfRangeException.ThrowIfNotEqual(uri.Collection, CollectionNsid.StarterPack);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        if (!await IsRecordOwnedBy(uri, Did, cancellationToken).ConfigureAwait(false))
        {
            throw new ArgumentException("Starter pack was not created by the current user.", nameof(uri));
        }

        return await PutRecord(
            record: starterPack,
            jsonSerializerOptions: BlueskyServer.BlueskyJsonSerializerOptions,
            collection: CollectionNsid.StarterPack,
            rKey: uri.RecordKey,
            swapRecord: swapRecord,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
