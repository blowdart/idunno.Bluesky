// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Graph;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Deletes the reference list opt-out record specified by <paramref name="uri"/>.
    /// </summary>
    /// <param name="uri">The <see cref="AtUri"/> of the reference list opt-out record.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="uri"/> is not in the correct collection.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> is <see langword="null" />.</exception>
    public async Task<AtProtoHttpResult<Commit>> DeleteReferenceListOptOut(AtUri uri, CancellationToken cancellationToken)
    {
        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        ArgumentNullException.ThrowIfNull(uri);

        if (uri.Collection != CollectionNsid.ReferenceListOptOut)
        {
            throw new ArgumentException($"{uri} is not in the {CollectionNsid.ReferenceListOptOut} collection.");
        }

        return await DeleteRecord(uri, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes the reference list opt-out record specified by <paramref name="uri"/>.
    /// </summary>
    /// <param name="uri">The <see cref="AtUri"/> of the reference list opt-out record.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    public async Task<AtProtoHttpResult<Commit>> DeleteReferenceListOptOut(AtUri uri)
    {
        return await DeleteReferenceListOptOut(uri, cancellationToken: default).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes the reference list opt-out record specified by <paramref name="record"/>.
    /// </summary>
    /// <param name="record">The <see cref="AtProtoRepositoryRecord{ReferenceListOptOut}"/> of the reference list opt-out record.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="record"/> is not in the correct collection.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="record"/> is <see langword="null" />.</exception>
    public async Task<AtProtoHttpResult<Commit>> DeleteReferenceListOptOut(AtProtoRepositoryRecord<ReferenceListOptOut> record, CancellationToken cancellationToken)
    {
        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        ArgumentNullException.ThrowIfNull(record);

        if (record.Uri.Collection != CollectionNsid.ReferenceListOptOut)
        {
            throw new ArgumentException($"record {record.Uri} is not in the {CollectionNsid.ReferenceListOptOut} collection.", nameof(record));
        }

        return await DeleteReferenceListOptOut(record.Uri, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes the reference list opt-out record specified by <paramref name="record"/>.
    /// </summary>
    /// <param name="record">The <see cref="AtProtoRepositoryRecord{ReferenceListOptOut}"/> of the reference list opt-out record.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    public async Task<AtProtoHttpResult<Commit>> DeleteReferenceListOptOut(AtProtoRepositoryRecord<ReferenceListOptOut> record)
    {
        return await DeleteReferenceListOptOut(record, cancellationToken: default).ConfigureAwait(false);
    }
}