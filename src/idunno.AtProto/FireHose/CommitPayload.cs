// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using PeterO.Cbor;

namespace idunno.AtProto.FireHose;

/// <summary>
/// Represents an update of repository state. Note that empty commits are allowed, which include no repo data changes, but an update to rev and signature.
/// </summary>
/// <remarks><para>See https://github.com/bluesky-social/atproto/blob/main/lexicons/com/atproto/sync/subscribeRepos.json for definition.</para></remarks>
public record CommitPayload : IFramePayload, ICborConvertor<CommitPayload>
{
    /// <summary>
    /// Creates a new instance of <see cref="CommitPayload"/>
    /// </summary>
    /// <param name="seq">The stream sequence number of this message.</param>
    /// <param name="rebase">Obsolete - unused.</param>
    /// <param name="tooBig">Obsolete - replaced by #sync event and data limits.</param>
    /// <param name="repo">The <see cref="Did">repo</see> this event comes from..</param>
    /// <param name="commit">The <paramref name="repo"/> commit object <see cref="Cid"/>.</param>
    /// <param name="rev">The <see cref="RecordKey">rev</see> of the emitted commit (if any).</param>
    /// <param name="since">The <see cref="RecordKey">rev</see> of the last emitted commit from this repo (if any).</param>
    /// <param name="rawBlocks">CAR file containing relevant blocks, as a diff since the previous repo state. </param>
    /// <param name="ops">List of repo mutation <see cref="RepoOp">operations</see> in this commit (eg, records created, updated, or deleted).</param>
    /// <param name="blobs">Obsolete, will soon be always empty. List of new blobs (by CID) referenced by records in this commit.</param>
    /// <param name="prevData">The root CID of the MST tree for the previous commit from this repo (indicated by the 'since' revision field in this message)</param>
    /// <param name="time"><see cref="DateTimeOffset"/> of when this message was originally broadcast.</param>
    public CommitPayload(
        long seq,
        bool rebase,
        bool tooBig,
        Did repo,
        Cid commit,
        RecordKey? rev,
        RecordKey? since,
        ReadOnlyMemory<byte>? rawBlocks,
        IEnumerable<RepoOp> ops,
        IEnumerable<Cid> blobs,
        Cid? prevData,
        DateTimeOffset time)
    {
        Seq = seq;
#pragma warning disable CS0618 // Type or member is obsolete
        Rebase = rebase;
        TooBig = tooBig;
#pragma warning restore CS0618 // Type or member is obsolete
        Repo = repo;
        Commit = commit;
        Rev = rev;
        Since = since;
        RawBlocks = rawBlocks;
#pragma warning disable CS0618 // Type or member is obsolete
        Blobs = blobs;
#pragma warning restore CS0618 // Type or member is obsolete
        Ops = new List<RepoOp>(ops).AsReadOnly();
#pragma warning disable CS0618 // Type or member is obsolete
        Blobs = new List<Cid>(blobs).AsReadOnly();
#pragma warning restore CS0618 // Type or member is obsolete
        PrevData = prevData;
        Time = time;
    }

    /// <summary>
    /// Gets the stream sequence number of this message.
    /// </summary>
    public long Seq { get; }

    /// <summary>
    /// Unused
    /// </summary>
    [SuppressMessage("Code Smell", "S1133: Deprecated code should be removed", Justification = "Keeping as its still part of the payload format.")]
    [Obsolete("Unused", UrlFormat = "https://github.com/bluesky-social/atproto/blob/main/lexicons/com/atproto/sync/subscribeRepos.json")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool Rebase { get; }

    /// <summary>
    /// Indicates that this commit contained too many ops, or data size was too large.
    /// </summary>
    /// <remarks>
    /// <para>Replaced by #sync event and data limits.</para>
    /// </remarks>
    [SuppressMessage("Code Smell", "S1133: Deprecated code should be removed", Justification = "Keeping as its still part of the payload format.")]
    [Obsolete("Replaced by #sync event and data limits.", UrlFormat = "https://github.com/bluesky-social/atproto/blob/main/lexicons/com/atproto/sync/subscribeRepos.json")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool TooBig { get; }

    /// <summary>
    /// Gets the <see cref="Did">repo</see> this event comes from..
    /// </summary>
    /// <remarks>
    ///<para> All other message types name this field 'did'</para>
    /// </remarks>
    public Did Repo { get; }

    /// <summary>
    /// Gets the Repo commit object <see cref="AtProto.Cid"/>
    /// </summary>
    public Cid Commit { get; }

    /// <summary>
    /// Gets the <see cref="RecordKey">rev</see> of the emitted commit.
    /// </summary>
    /// <remarks>
    /// <para>Note that this information is also in the commit object included in blocks, unless this is a deprecated tooBig event.</para>
    /// </remarks>
    public RecordKey? Rev { get; }

    /// <summary>
    /// Gets the <see cref="RecordKey">rev</see> of the last emitted commit from this repo (if any).
    /// </summary>
    public RecordKey? Since { get; }

    /// <summary>
    /// Gets the CAR file containing relevant blocks, as a diff since the previous repo state.
    /// The commit must be included as a block, and the commit block CID must be the first entry in the CAR header 'roots' list.
    /// </summary>
    public ReadOnlyMemory<byte>? RawBlocks { get; }

    /// <summary>
    /// Gets a list of repo mutation <see cref="RepoOp">operations</see> in this commit (eg, records created, updated, or deleted).
    /// </summary>
    public IReadOnlyList<RepoOp> Ops { get; }

    /// <summary>
    /// List of new blobs (by CID) referenced by records in this commit.
    /// </summary>
    /// <remarks>
    ///<para>DEPRECATED -- will soon always be empty.</para>
    /// </remarks>
    [SuppressMessage("Code Smell", "S1133: Deprecated code should be removed", Justification = "Keeping as its still part of the payload format.")]
    [Obsolete("Will soon always be empty. List of new blobs (by CID) referenced by records in this commit.",
        UrlFormat = "https://github.com/bluesky-social/atproto/blob/main/lexicons/com/atproto/sync/subscribeRepos.json")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public IEnumerable<Cid> Blobs { get; }

    /// <summary>
    /// Gets the root CID of the MST tree for the previous commit from this repo (indicated by the 'since' revision field in this message).
    /// Corresponds to the 'data' field in the repo commit object.
    /// </summary>
    /// <remarks>
    ///<para>This field is effectively required for the 'inductive' version of firehose.</para>
    /// </remarks>
    public Cid? PrevData { get; }

    /// <summary>
    /// Gets the <see cref="DateTimeOffset"/> of when this message was originally broadcast.
    /// </summary>
    public DateTimeOffset Time { get; }

    /// <summary>
    /// Gets the CAR file containing relevant blocks, as a diff since the previous repo state.
    /// The commit must be included as a block, and the commit block CID must be the first entry in the CAR header 'roots' list.
    /// </summary>
    public AtContentAddressableArchive? Blocks { get; }

    /// <inheritdoc/>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="cborObject"/> is <see langword="null" />.</exception>
    public static CommitPayload FromCBORObject(CBORObject cborObject)
    {
        ArgumentNullException.ThrowIfNull(cborObject);

        long seq = cborObject["seq"].AsInt64Value();
        bool rebase = cborObject["rebase"].AsBoolean();
        bool tooBig = cborObject["tooBig"].AsBoolean();
        Did repo = new (cborObject["repo"].AsString());
        Cid commit = new (cborObject["commit"].GetByteString());
        RecordKey? rev = cborObject["rev"].ToRecordKey();
        RecordKey? since = cborObject["since"].ToRecordKey();
        Cid? prevData = cborObject["prevData"].ToCid();
        DateTimeOffset time = cborObject["time"].ToDateTimeOffset();

        List<Cid> blobs = [];
        if (cborObject["blobs"] is not null)
        {
            foreach (CBORObject blob in cborObject["blobs"].Values.Where(blobs => blobs != null))
            {
                blobs.Add(new Cid(blob.GetByteString()));
            }
        }

        List<RepoOp> ops = [];
        if (cborObject["ops"] is not null)
        {
            foreach(CBORObject op in cborObject["ops"].Values.Where(ops => ops != null))
            {
                ops.Add(new RepoOp(op));
            }
        }

        byte[]? rawBlocks = null;
        if (cborObject["blocks"] is not null && !cborObject["blocks"].IsNull && cborObject["blocks"].Type == CBORType.ByteString)
        {
            rawBlocks = cborObject["blocks"].GetByteString();
        }

        return new CommitPayload(
              seq: seq,
              rebase: rebase,
              tooBig: tooBig,
              repo: repo,
              commit: commit,
              rev: rev,
              since: since,
              rawBlocks: rawBlocks,
              ops: ops,
              blobs: blobs,
              prevData: prevData,
              time: time);
    }
}


