// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

namespace idunno.AtProto.Firehose;

/// <summary>
/// Encapsulates a single record operation within a <see cref="FirehoseCommitEvent"/>.
/// </summary>
public sealed record FirehoseRepoOperation
{
    /// <summary>
    /// Creates a new instance of <see cref="FirehoseRepoOperation"/>.
    /// </summary>
    /// <param name="action">The action performed on the record.</param>
    /// <param name="path">The repository path of the record.</param>
    /// <param name="collection">The collection of the record.</param>
    /// <param name="recordKey">The record key of the record.</param>
    /// <param name="cid">The content identifier of the new record, if any.</param>
    /// <param name="prev">The content identifier of the previous version of the record, if any.</param>
    /// <param name="recordData">The DAG-CBOR encoded record, if any.</param>
    internal FirehoseRepoOperation(
        FirehoseRepoAction action,
        string path,
        Nsid collection,
        RecordKey recordKey,
        Cid? cid,
        Cid? prev,
        ReadOnlyMemory<byte>? recordData)
    {
        Action = action;
        Path = path;
        Collection = collection;
        RecordKey = recordKey;
        Cid = cid;
        Prev = prev;
        RecordData = recordData;
    }

    /// <summary>
    /// Gets the action performed on the record.
    /// </summary>
    public FirehoseRepoAction Action { get; }

    /// <summary>
    /// Gets the repository path of the record, in the form <c>collection/recordKey</c>.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets the collection of the record.
    /// </summary>
    public Nsid Collection { get; }

    /// <summary>
    /// Gets the record key of the record.
    /// </summary>
    public RecordKey RecordKey { get; }

    /// <summary>
    /// Gets the content identifier of the new version of the record.
    /// </summary>
    /// <value>The content identifier for creates and updates, or <see langword="null"/> for deletes.</value>
    public Cid? Cid { get; }

    /// <summary>
    /// Gets the content identifier of the previous version of the record.
    /// </summary>
    /// <value>The content identifier for updates and deletes, if the server sent it, or <see langword="null"/> for creates.</value>
    public Cid? Prev { get; }

    /// <summary>
    /// Gets the DAG-CBOR encoded record.
    /// </summary>
    /// <value>The encoded record for creates and updates, or <see langword="null"/> for deletes.</value>
    /// <remarks>
    /// <para>The data is taken from the commit's CAR blocks, and its content identifier has been checked against <see cref="Cid"/>.</para>
    /// </remarks>
    public ReadOnlyMemory<byte>? RecordData { get; }

    /// <summary>
    /// Converts <see cref="RecordData"/> to its JSON representation.
    /// </summary>
    /// <returns>The JSON representation of the record, or <see langword="null"/> if the operation carries no record.</returns>
    /// <exception cref="InvalidDataException">The record is not valid DAG-CBOR.</exception>
    /// <remarks>
    /// <para>The conversion is performed on every call, so cache the result if it is needed more than once.
    /// The record content comes from the repository owner and is untrusted.</para>
    /// </remarks>
    // Not cached. This is a record type, so a cache field would take part in the generated Equals and GetHashCode.
    public JsonElement? GetRecord() => RecordData is ReadOnlyMemory<byte> data ? DagCbor.ToJsonElement(data) : null;
}
