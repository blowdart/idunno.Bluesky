// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Record;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Creates a record in the specified collection belonging to the current user.
    /// </summary>
    /// <typeparam name="TRecord">The type of record to create.</typeparam>
    /// <param name="record"><para>The record to be created.</para></param>
    /// <param name="collection"><para>The collection the record should be created in.</para></param>
    /// <param name="rKey"><para>An optional <see cref="RecordKey"/> to create the record with.</para></param>
    /// <param name="validate">
    ///   <para>Sets a flag indicating what validation will be performed, if any.</para>
    ///   <para>A value of <see langword="true"/> requires lexicon schema validation of record data.</para>
    ///   <para>A value of <see langword="false"/> will skip Lexicon schema validation of record data.</para>
    ///   <para>A value of <see langword="null"/> to validate record data only for known lexicons.</para>
    ///   <para>Defaults to <see langword="true"/>.</para>
    /// </param>
    /// <param name="swapCommit"><para>Compare and swap with the previous commit by CID.</para></param>
    /// <param name="serviceProxy"><para>The service the PDS should proxy the call to, if any.</para></param>
    /// <param name="cancellationToken"><para>A cancellation token that can be used by other objects or threads to receive notice of cancellation.</para></param>
    /// <returns><para>The task object representing the asynchronous operation.</para></returns>
    /// <exception cref="ArgumentNullException"><para>Thrown when <paramref name="record"/> or <paramref name="collection"/> is <see langword="null"/>.</para></exception>
    /// <exception cref="AuthenticationRequiredException"><para>Thrown when the current agent is not authenticated.</para></exception>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    public async Task<AtProtoHttpResult<CreateRecordResult>> CreateBlueskyRecord<TRecord>(
        TRecord record,
        Nsid collection,
        RecordKey? rKey = null,
        bool? validate = true,
        Cid? swapCommit = null,
        string? serviceProxy = null,
        CancellationToken cancellationToken = default) where TRecord : BlueskyRecord
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(collection);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        AtProtoHttpResult<CreateRecordResult> result = await CreateRecord(
            record: record,
            jsonSerializerOptions: BlueskyServer.BlueskyJsonSerializerOptions,
            collection: collection,
            rKey: rKey,
            validate: validate,
            swapCommit: swapCommit,
            serviceProxy: serviceProxy,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return result;
    }

    /// <summary>
    /// Gets the Bluesky record specified by the identifying parameters.
    /// </summary>
    /// <typeparam name="TRecord">The type of record to get.</typeparam>
    /// <param name="repo">The <see cref="AtIdentifier"/> of the repo to retrieve the record from.</param>
    /// <param name="collection">The NSID of the collection the record should be retrieved from.</param>
    /// <param name="rKey">The record key, identifying the record to be retrieved.</param>
    /// <param name="cid">The CID of the version of the record. If not specified, then return the most recent version.</param>
    /// <param name="serviceProxy">The service the PDS should proxy the call to, if any.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="repo"/>, <paramref name="collection"/> is <see langword="null"/> or empty.</exception>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    public async Task<AtProtoHttpResult<AtProtoRepositoryRecord<TRecord>>> GetBlueskyRecord<TRecord>(
        AtIdentifier repo,
        Nsid collection,
        RecordKey rKey,
        Cid? cid = null,
        string? serviceProxy = null,
        CancellationToken cancellationToken = default) where TRecord : BlueskyRecord
    {
        ArgumentNullException.ThrowIfNull(repo);
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(rKey);

        return await GetRecord<TRecord>(
            repo: repo,
            collection: collection,
            rKey: rKey,
            cid: cid,
            jsonSerializerOptions: BlueskyServer.BlueskyJsonSerializerOptions,
            serviceProxy: serviceProxy,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the Bluesky record specified by the <paramref name="uri"/>.
    /// </summary>
    /// <typeparam name="TRecord">The type of record to get.</typeparam>
    /// <param name="uri">The <see cref="AtUri"/> of the record to retrieve.</param>
    /// <param name="serviceProxy">The service the PDS should proxy the call to, if any.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/>, or its collection or rkey property is <see langword="null"/>.</exception>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    public async Task<AtProtoHttpResult<AtProtoRepositoryRecord<TRecord>>> GetBlueskyRecord<TRecord>(
        AtUri uri,
        string? serviceProxy = null,
        CancellationToken cancellationToken = default) where TRecord : BlueskyRecord
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(uri.Collection);
        ArgumentNullException.ThrowIfNull(uri.RecordKey);

        return await GetBlueskyRecord<TRecord>(
            repo: uri.Repo,
            collection: uri.Collection,
            rKey: uri.RecordKey,
            cid: null,
            serviceProxy: serviceProxy,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets a page of records in the specified <paramref name="collection"/>.
    /// </summary>
    /// <typeparam name="TRecord">The type of the record value to get.</typeparam>
    /// <param name="repo">The <see cref="AtIdentifier"/> of the repo to retrieve the records from.</param>
    /// <param name="collection">The NSID of the collection the records should be retrieved from.</param>
    /// <param name="limit">The number of records to return in each page.</param>
    /// <param name="cursor">The cursor position to start retrieving records from.</param>
    /// <param name="reverse">A flag indicating if records should be listed in reverse order.</param>
    /// <param name="service">The service to retrieve the records from. If <see langword="null" /> it will be resolved from <paramref name="repo"/>.</param>
    /// <param name="serviceProxy">The service the PDS should proxy the call to, if any.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="repo"/> or <paramref name="collection"/> is <see langword="null"/>.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current session is not an authenticated session.</exception>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the BlueskyServer.BlueskyJsonSerializerOptions passed to ListRecords().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the BlueskyServer.BlueskyJsonSerializerOptions passed to ListRecords().")]
    public async Task<AtProtoHttpResult<PagedReadOnlyCollection<AtProtoRepositoryRecord<TRecord>>>> ListBlueskyRecords<TRecord>(
        AtIdentifier repo,
        Nsid collection,
        int? limit = 50,
        string? cursor = null,
        bool reverse = false,
        Uri? service = null,
        string? serviceProxy = null,
        CancellationToken cancellationToken = default) where TRecord : BlueskyRecord
    {
        return await ListRecords<TRecord>(
            repo: repo,
            collection: collection,
            jsonSerializerOptions: BlueskyServer.BlueskyJsonSerializerOptions,
            limit: limit,
            cursor: cursor,
            reverse: reverse,
            service: service,
            serviceProxy: serviceProxy,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}