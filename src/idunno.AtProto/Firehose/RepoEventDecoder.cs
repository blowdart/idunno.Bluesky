// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Formats.Cbor;
using System.Globalization;

using idunno.AtProto.Repo;
using idunno.AtProto.Sync;

namespace idunno.AtProto.Firehose;

/// <summary>
/// Decodes <c>com.atproto.sync.subscribeRepos</c> payloads.
/// </summary>
/// <param name="options">The options which set the decoding limits.</param>
/// <param name="verifier">The signature verifier, or <see langword="null"/> to skip signature verification.</param>
internal sealed class RepoEventDecoder(FirehoseOptions options, FirehoseSignatureVerifier? verifier) : IFirehosePayloadDecoder
{
    /// <summary>
    /// The NSID of the endpoint.
    /// </summary>
    internal const string EndpointNsid = "com.atproto.sync.subscribeRepos";

    /// <summary>
    /// The lexicon <c>maxLength</c> of a commit's <c>blocks</c>.
    /// </summary>
    internal const int MaximumCommitBlocksLength = 2_000_000;

    /// <summary>
    /// The lexicon <c>maxLength</c> of a sync event's <c>blocks</c>.
    /// </summary>
    internal const int MaximumSyncBlocksLength = 10_000;

    /// <summary>
    /// The lexicon <c>maxLength</c> of a commit's <c>ops</c>.
    /// </summary>
    internal const int MaximumOperations = 200;

    // The lexicon does not bound the deprecated blobs array, so it is held to the operation limit to stop a hostile frame forcing large allocations.
    internal const int MaximumBlobs = 200;

    private const string CommitType = "#commit";
    private const string SyncType = "#sync";
    private const string IdentityType = "#identity";
    private const string AccountType = "#account";

    private static readonly CborFieldNames s_payloadFields = new(
        "active", "blobs", "blocks", "commit", "did", "handle", "ops", "prevData", "rebase", "repo", "rev", "since", "status", "time", "tooBig");

    private static readonly CborFieldNames s_operationFields = new("action", "cid", "path", "prev");

    private static readonly CborFieldNames s_commitFields = new("data", "rev");

    /// <inheritdoc/>
    public string Nsid => EndpointNsid;

    /// <inheritdoc/>
    public CborFieldNames PayloadFields => s_payloadFields;

    /// <inheritdoc/>
    public bool IsSequenced(string type) => type is CommitType or SyncType or IdentityType or AccountType;

    /// <inheritdoc/>
    public Did? GetSubject(string type, CborFields fields)
    {
        try
        {
            return fields.GetDid(type == CommitType ? "repo" : "did");
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public Task<FirehoseEvent> DecodeAsync(string type, long sequence, CborFields fields, CancellationToken cancellationToken) => type switch
    {
        CommitType => DecodeCommitAsync(sequence, fields, cancellationToken),
        SyncType => DecodeSyncAsync(sequence, fields, cancellationToken),
        IdentityType => Task.FromResult<FirehoseEvent>(DecodeIdentity(sequence, fields)),
        AccountType => Task.FromResult<FirehoseEvent>(DecodeAccount(sequence, fields)),
        _ => throw new InvalidDataException($"The message type '{type}' is not supported.")
    };

    private FirehoseIdentityEvent DecodeIdentity(long sequence, CborFields fields)
    {
        // The handle is passed on unvalidated, so it is sanitized like other server text to stop it corrupting logs or consoles.
        FirehoseIdentityEvent identity = new(sequence, fields.GetDid("did"), fields.GetDateTime("time"), EventStreamReader.Sanitize(fields.GetOptionalString("handle")));

        // An identity event can announce a rotated signing key, so any cached key is dropped rather than trusted until it expires.
        // A malicious server can abuse this to force a resolution for every event, but only when signature verification is on.
        verifier?.Invalidate(identity.Did);

        return identity;
    }

    private static FirehoseAccountEvent DecodeAccount(long sequence, CborFields fields)
    {
        string? status = fields.GetOptionalString("status");

        return new FirehoseAccountEvent(
            sequence,
            fields.GetDid("did"),
            fields.GetDateTime("time"),
            fields.GetBoolean("active"),
            status is null ? null : RepoStatusConverter.Parse(status));
    }

    private async Task<FirehoseEvent> DecodeSyncAsync(long sequence, CborFields fields, CancellationToken cancellationToken)
    {
        Did did = fields.GetDid("did");
        string rev = GetRevision(fields, "rev");
        DateTimeOffset time = fields.GetDateTime("time");
        byte[] blocks = fields.GetBytes("blocks", MaximumSyncBlocksLength);

        (Cid root, Dictionary<Cid, ReadOnlyMemory<byte>> carBlocks) = ReadCar(blocks);
        Cid data = await ValidateCommitBlockAsync(root, carBlocks, did, rev, cancellationToken).ConfigureAwait(false);

        return new FirehoseSyncEvent(sequence, did, rev, root, data, time, blocks);
    }

    private async Task<FirehoseEvent> DecodeCommitAsync(long sequence, CborFields fields, CancellationToken cancellationToken)
    {
        Did repo = fields.GetDid("repo");
        Cid commit = fields.GetCidLink("commit");
        string rev = GetRevision(fields, "rev");

        if (!fields.Contains("since"))
        {
            throw new InvalidDataException("The required 'since' field is missing.");
        }

        string? since = fields.IsAbsentOrNull("since") ? null : GetRevision(fields, "since");
        DateTimeOffset time = fields.GetDateTime("time");
        bool rebase = fields.GetBoolean("rebase");
        bool tooBig = fields.GetBoolean("tooBig");
        Cid? prevData = fields.GetOptionalCidLink("prevData");
        byte[] blocks = fields.GetBytes("blocks", MaximumCommitBlocksLength);
        IReadOnlyList<ReadOnlyMemory<byte>> encodedOperations = fields.GetArray("ops", MaximumOperations);
        IReadOnlyList<ReadOnlyMemory<byte>> encodedBlobs = fields.GetArray("blobs", MaximumBlobs);

        List<Cid> blobs = new(encodedBlobs.Count);
        foreach (ReadOnlyMemory<byte> encodedBlob in encodedBlobs)
        {
            blobs.Add(FirehoseCbor.Wrap(() => FirehoseCbor.ReadCidLink(new CborReader(encodedBlob, CborConformanceMode.Canonical))));
        }

        (Cid root, Dictionary<Cid, ReadOnlyMemory<byte>> carBlocks) = ReadCar(blocks);

        if (root != commit)
        {
            throw new InvalidDataException("The first CAR root is not the commit.");
        }

        Cid data = await ValidateCommitBlockAsync(root, carBlocks, repo, rev, cancellationToken).ConfigureAwait(false);

        List<FirehoseRepoOperation> operations = new(encodedOperations.Count);
        foreach (ReadOnlyMemory<byte> encodedOperation in encodedOperations)
        {
            operations.Add(DecodeOperation(FirehoseCbor.ReadFields(encodedOperation, s_operationFields), carBlocks));
        }

        return new FirehoseCommitEvent(
            sequence,
            repo,
            commit,
            rev,
            since,
            data,
            prevData,
            time,
            operations.AsReadOnly(),
            blocks,
            rebase,
            tooBig,
            blobs.AsReadOnly());
    }

    private static FirehoseRepoOperation DecodeOperation(CborFields fields, Dictionary<Cid, ReadOnlyMemory<byte>> carBlocks)
    {
        string actionName = fields.GetString("action");
        FirehoseRepoAction action = actionName switch
        {
            "create" => FirehoseRepoAction.Create,
            "update" => FirehoseRepoAction.Update,
            "delete" => FirehoseRepoAction.Delete,
            _ => FirehoseRepoAction.Unknown
        };

        string path = fields.GetString("path");
        (Nsid collection, RecordKey recordKey) = ParsePath(path);

        if (!fields.Contains("cid"))
        {
            throw new InvalidDataException($"The operation on '{path}' has no 'cid' field.");
        }

        Cid? cid = fields.GetOptionalCidLink("cid");

        // A create has no previous version, so prev is left undefined rather than null. Everything else either
        // links a previous version or leaves the field out.
        if (fields.Contains("prev") && (action == FirehoseRepoAction.Create || fields.IsAbsentOrNull("prev")))
        {
            throw new InvalidDataException($"The operation on '{path}' has an unexpected 'prev' field.");
        }

        Cid? prev = fields.GetOptionalCidLink("prev");

        switch (action)
        {
            case FirehoseRepoAction.Create or FirehoseRepoAction.Update when cid is null:
                throw new InvalidDataException($"The {actionName} operation on '{path}' has no record CID.");

            case FirehoseRepoAction.Delete when cid is not null:
                throw new InvalidDataException($"The delete operation on '{path}' has a record CID.");
        }

        ReadOnlyMemory<byte>? recordData = null;

        if (cid is not null)
        {
            if (!carBlocks.TryGetValue(cid, out ReadOnlyMemory<byte> block))
            {
                throw new InvalidDataException($"The record block for '{path}' is missing from the commit.");
            }

            recordData = block;
        }

        return new FirehoseRepoOperation(action, path, collection, recordKey, cid, prev, recordData);
    }

    private static (AtProto.Nsid Collection, RecordKey RecordKey) ParsePath(string path)
    {
        int separator = path.IndexOf('/', StringComparison.Ordinal);

        if (separator > 0 &&
            separator == path.LastIndexOf('/') &&
            AtProto.Nsid.TryParse(path[..separator], out AtProto.Nsid? collection) &&
            collection is not null &&
            RecordKey.TryParse(path[(separator + 1)..], out RecordKey? recordKey) &&
            recordKey is not null)
        {
            return (collection, recordKey);
        }

        throw new InvalidDataException("An operation path is not a valid collection and record key.");
    }

    private static string GetRevision(CborFields fields, string name)
    {
        string value = fields.GetString(name);

        return TimestampIdentifier.TryParse(value, out _)
            ? value
            : throw new InvalidDataException($"The '{name}' field is not a valid TID.");
    }

    private (Cid Root, Dictionary<Cid, ReadOnlyMemory<byte>> Blocks) ReadCar(byte[] car)
    {
        try
        {
            return FirehoseCbor.Wrap(() =>
            {
                using CarReader reader = new(new MemoryStream(car, writable: false), options.MaximumCarBlockSize);
                CarHeader header = reader.ReadHeader();

                if (header.Roots.Count == 0)
                {
                    throw new InvalidDataException("The CAR has no roots.");
                }

                Dictionary<Cid, ReadOnlyMemory<byte>> blocks = [];
                int count = 0;

                while (reader.ReadBlock() is CarBlock block)
                {
                    if (++count > options.MaximumCarBlocks)
                    {
                        throw new InvalidDataException(
                            string.Create(CultureInfo.InvariantCulture, $"The CAR has more than {options.MaximumCarBlocks} blocks."));
                    }

                    if (block.Cid != Cid.FromDagCbor(block.Data.Span))
                    {
                        throw new InvalidDataException($"The CAR block '{block.Cid}' does not match its content.");
                    }

                    blocks.TryAdd(block.Cid, block.Data);
                }

                return (header.Roots[0], blocks);
            });
        }
        catch (EndOfStreamException exception)
        {
            throw new InvalidDataException("The CAR is truncated.", exception);
        }
    }

    private async Task<Cid> ValidateCommitBlockAsync(
        Cid root,
        Dictionary<Cid, ReadOnlyMemory<byte>> carBlocks,
        Did did,
        string rev,
        CancellationToken cancellationToken)
    {
        if (!carBlocks.TryGetValue(root, out ReadOnlyMemory<byte> commitBlock))
        {
            throw new InvalidDataException("The commit block is missing from the CAR.");
        }

        (Did commitDid, byte[] unsignedCommit, byte[] signature) = FirehoseCbor.Wrap(() => CarReader.ReadUnsignedCommit(commitBlock));

        if (commitDid != did)
        {
            throw new InvalidDataException("The commit DID does not match the event.");
        }

        CborFields commitFields = FirehoseCbor.ReadFields(commitBlock, s_commitFields);

        if (!string.Equals(commitFields.GetString("rev"), rev, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The commit revision does not match the event.");
        }

        Cid data = commitFields.GetCidLink("data");

        if (verifier is not null)
        {
            await verifier.VerifyAsync(
                did,
                SigningKeyVerifier.RepositorySigningKeyFragment,
                unsignedCommit,
                signature,
                "repository commit",
                [],
                cancellationToken).ConfigureAwait(false);
        }

        return data;
    }
}
