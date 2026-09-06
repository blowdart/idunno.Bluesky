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
    /// Updates the list record referenced by its <paramref name="uri"/>.
    /// </summary>
    /// <param name="uri">The <see cref="AtUri"/> of the list record to update.</param>
    /// <param name="list">The <see cref="List"/> to update the record with</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> or its Collection or RecordKey property is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="uri"/> does not point to a list.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Put().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Put().")]
    public async Task<AtProtoHttpResult<PutRecordResult>> UpdateList(
        AtUri uri,
        List list,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(uri.Collection);
        ArgumentNullException.ThrowIfNull(uri.RecordKey);
        ArgumentOutOfRangeException.ThrowIfNotEqual(uri.Collection, CollectionNsid.List);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await PutRecord<BlueskyTimestampedRecord>(
            record: list,
            collection: CollectionNsid.List,
            rKey: uri.RecordKey,
            jsonSerializerOptions: BlueskyServer.BlueskyJsonSerializerOptions,
            validate: true,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Updates the referenced list record.
    /// </summary>
    /// <param name="list">The <see cref="AtProtoRepositoryRecord{TRecord}"/> referenced <see cref="List"/> to update.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="list"/> or its Uri, or the URI Collection or RecordKey property is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="list"/> does not point to a list.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<PutRecordResult>> UpdateList(
        AtProtoRepositoryRecord<List> list)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(list.Uri);
        ArgumentNullException.ThrowIfNull(list.Uri.Collection);
        ArgumentNullException.ThrowIfNull(list.Uri.RecordKey);
        ArgumentOutOfRangeException.ThrowIfNotEqual(list.Uri.Collection, CollectionNsid.List);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await UpdateList(
            uri: list.Uri,
            list: list.Value,
            cancellationToken: default).ConfigureAwait(false);
    }

    /// <summary>
    /// Updates the referenced list record.
    /// </summary>
    /// <param name="list">The <see cref="AtProtoRepositoryRecord{TRecord}"/> referenced <see cref="List"/> to update.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="list"/> or its Uri, or the URI Collection or RecordKey property is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="list"/> does not point to a list.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<PutRecordResult>> UpdateList(
        AtProtoRepositoryRecord<List> list,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(list.Uri);
        ArgumentNullException.ThrowIfNull(list.Uri.Collection);
        ArgumentNullException.ThrowIfNull(list.Uri.RecordKey);
        ArgumentOutOfRangeException.ThrowIfNotEqual(list.Uri.Collection, CollectionNsid.List);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await UpdateList(
            uri: list.Uri,
            list: list.Value,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Updates the list record referenced by its <paramref name="uri"/>.
    /// </summary>
    /// <param name="uri">The <see cref="AtUri"/> of the list record to update.</param>
    /// <param name="list">The <see cref="List"/> to update the record with</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> or its Collection or RecordKey property is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="uri"/> does not point to a list.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Put().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Put().")]
    public async Task<AtProtoHttpResult<PutRecordResult>> UpdateList(
        AtUri uri,
        List list)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(uri.Collection);
        ArgumentNullException.ThrowIfNull(uri.RecordKey);
        ArgumentOutOfRangeException.ThrowIfNotEqual(uri.Collection, CollectionNsid.List);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await UpdateList(
            uri: uri,
            list: list,
            cancellationToken: default).ConfigureAwait(false);
    }
}
