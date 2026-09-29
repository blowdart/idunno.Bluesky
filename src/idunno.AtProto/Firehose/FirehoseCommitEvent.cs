// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

namespace idunno.AtProto.Firehose;

/// <summary>
/// Encapsulates a <c>#commit</c> event, which describes a change to a repository.
/// </summary>
/// <remarks>
/// <para>Before an event is surfaced its CAR blocks have been checked against their content identifiers, the commit block
/// has been checked to be the first CAR root, and the commit's DID and revision have been checked against the event.
/// Signatures are only checked when <see cref="FirehoseOptions.VerifySignatures"/> is set.</para>
/// <para>The Merkle search tree is not checked against <see cref="PrevData"/>.</para>
/// <para>Commits are not checked against the previous commit for the same repository, so a relay can send a repository's
/// commits out of <see cref="Rev"/> order, replay one, or skip one. An application which depends on per-repository order
/// has to track each repository's last <see cref="Rev"/> itself. That state grows with every repository on the network, so
/// at firehose volume it is expensive and has to be bounded.</para>
/// </remarks>
[SuppressMessage("Major Code Smell", "S1133", Justification = "The deprecated fields are still required by the lexicon.")]
public sealed record FirehoseCommitEvent : FirehoseEvent
{
    /// <summary>
    /// Creates a new instance of <see cref="FirehoseCommitEvent"/>.
    /// </summary>
    /// <param name="sequence">The sequence number of the event.</param>
    /// <param name="repo">The DID of the repository.</param>
    /// <param name="commit">The content identifier of the commit.</param>
    /// <param name="rev">The revision of the commit.</param>
    /// <param name="since">The revision of the previous commit, if any.</param>
    /// <param name="data">The root of the repository's Merkle search tree.</param>
    /// <param name="prevData">The root of the previous commit's Merkle search tree, if any.</param>
    /// <param name="time">When the event was emitted.</param>
    /// <param name="operations">The record operations in the commit.</param>
    /// <param name="blocks">The CAR file carrying the changed blocks.</param>
    /// <param name="rebase">The deprecated rebase flag.</param>
    /// <param name="tooBig">The deprecated too big flag.</param>
    /// <param name="blobs">The deprecated blob list.</param>
    internal FirehoseCommitEvent(
        long sequence,
        Did repo,
        Cid commit,
        string rev,
        string? since,
        Cid data,
        Cid? prevData,
        DateTimeOffset time,
        IReadOnlyList<FirehoseRepoOperation> operations,
        ReadOnlyMemory<byte> blocks,
        bool rebase,
        bool tooBig,
        IReadOnlyList<Cid> blobs) : base(sequence)
    {
        Repo = repo;
        Commit = commit;
        Rev = rev;
        Since = since;
        Data = data;
        PrevData = prevData;
        Time = time;
        Operations = operations;
        Blocks = blocks;
#pragma warning disable CS0618 // Type or member is obsolete
        Rebase = rebase;
        TooBig = tooBig;
        Blobs = blobs;
#pragma warning restore CS0618 // Type or member is obsolete
    }

    /// <summary>
    /// Gets the DID of the repository the commit belongs to.
    /// </summary>
    public Did Repo { get; }

    /// <summary>
    /// Gets the content identifier of the commit.
    /// </summary>
    public Cid Commit { get; }

    /// <summary>
    /// Gets the revision of the commit.
    /// </summary>
    public string Rev { get; }

    /// <summary>
    /// Gets the revision of the previous commit, if any.
    /// </summary>
    /// <value>The revision of the commit this one follows, or <see langword="null"/> if there is none.</value>
    public string? Since { get; }

    /// <summary>
    /// Gets the root of the repository's Merkle search tree after the commit, as recorded in the commit block.
    /// </summary>
    public Cid Data { get; }

    /// <summary>
    /// Gets the root of the repository's Merkle search tree before the commit, if the server sent it.
    /// </summary>
    public Cid? PrevData { get; }

    /// <summary>
    /// Gets when the event was emitted by the upstream server.
    /// </summary>
    public DateTimeOffset Time { get; }

    /// <summary>
    /// Gets the record operations in the commit.
    /// </summary>
    public IReadOnlyList<FirehoseRepoOperation> Operations { get; }

    /// <summary>
    /// Gets the CAR file carrying the blocks which changed in the commit.
    /// </summary>
    public ReadOnlyMemory<byte> Blocks { get; }

    /// <summary>
    /// Gets the deprecated rebase flag.
    /// </summary>
    [Obsolete("Deprecated by the com.atproto.sync.subscribeRepos lexicon. Current servers always send false.")]
    public bool Rebase { get; }

    /// <summary>
    /// Gets the deprecated too big flag.
    /// </summary>
    [Obsolete("Deprecated by the com.atproto.sync.subscribeRepos lexicon. Current servers always send false.")]
    public bool TooBig { get; }

    /// <summary>
    /// Gets the deprecated list of blobs referenced by the commit.
    /// </summary>
    /// <remarks>
    /// <para>The lexicon does not limit this list, but a commit with more than 200 blobs is surfaced as a <see cref="FirehoseInvalidEvent"/>.</para>
    /// </remarks>
    [Obsolete("Deprecated by the com.atproto.sync.subscribeRepos lexicon. Current servers send an empty list.")]
    public IReadOnlyList<Cid> Blobs { get; }
}
