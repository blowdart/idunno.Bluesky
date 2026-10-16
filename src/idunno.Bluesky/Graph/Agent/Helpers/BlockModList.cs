// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Graph;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Creates a block record in the authenticated user's repo for every actor in the specified moderation list. Requires authentication.
    /// </summary>
    /// <param name="listUri">The <see cref="AtUri"/> of the moderation list of actors to block.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="listUri"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="listUri"/> does not point to a list record.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is unauthenticated.</exception>
    public async Task<AtProtoHttpResult<CreateRecordResult>> BlockModList(
        AtUri listUri,
        CancellationToken cancellationToken = default)
    {
        return await BlockModList(rKey: null, listUri, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a block record for the specified moderation list using the specified <paramref name="rKey"/>.
    /// </summary>
    /// <param name="rKey">The record key to use for the list block record.</param>
    /// <param name="listUri">The <see cref="AtUri"/> of the moderation list of actors to block.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="listUri"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="listUri"/> does not point to a list record.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is unauthenticated.</exception>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "The caller-supplied-key overload preserves the existing signature and cancellation-token call patterns.")]
    public async Task<AtProtoHttpResult<CreateRecordResult>> BlockModList(
        RecordKey? rKey,
        AtUri listUri,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(listUri);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        if (listUri.Collection != CollectionNsid.List)
        {
            throw new ArgumentException($"listUri does not point to an {CollectionNsid.List} record", nameof(listUri));
        }

        ListBlock listBlock = new(listUri);

        return await CreateBlueskyRecord(
            listBlock,
            collection: CollectionNsid.ListBlock,
            rKey: rKey,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
