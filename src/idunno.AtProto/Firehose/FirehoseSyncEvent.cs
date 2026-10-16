// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Firehose;

/// <summary>
/// Encapsulates a <c>#sync</c> event, which asserts the current state of a repository without describing how it got there.
/// </summary>
/// <remarks>
/// <para>A sync event tells a consumer to discard any state it holds for the repository and resynchronize from <see cref="Commit"/>.</para>
/// </remarks>
public sealed record FirehoseSyncEvent : FirehoseEvent
{
    /// <summary>
    /// Creates a new instance of <see cref="FirehoseSyncEvent"/>.
    /// </summary>
    /// <param name="sequence">The sequence number of the event.</param>
    /// <param name="did">The DID of the repository.</param>
    /// <param name="rev">The revision of the commit.</param>
    /// <param name="commit">The content identifier of the commit.</param>
    /// <param name="data">The root of the repository's Merkle search tree.</param>
    /// <param name="time">When the event was emitted.</param>
    /// <param name="blocks">The CAR file carrying the commit block.</param>
    internal FirehoseSyncEvent(long sequence, Did did, string rev, Cid commit, Cid data, DateTimeOffset time, ReadOnlyMemory<byte> blocks) : base(sequence)
    {
        Did = did;
        Rev = rev;
        Commit = commit;
        Data = data;
        Time = time;
        Blocks = blocks;
    }

    /// <summary>
    /// Gets the DID of the repository.
    /// </summary>
    public Did Did { get; }

    /// <summary>
    /// Gets the revision of the commit.
    /// </summary>
    public string Rev { get; }

    /// <summary>
    /// Gets the content identifier of the commit, which is the first root of <see cref="Blocks"/>.
    /// </summary>
    public Cid Commit { get; }

    /// <summary>
    /// Gets the root of the repository's Merkle search tree, as recorded in the commit block.
    /// </summary>
    public Cid Data { get; }

    /// <summary>
    /// Gets when the event was emitted by the upstream server.
    /// </summary>
    public DateTimeOffset Time { get; }

    /// <summary>
    /// Gets the CAR file carrying the commit block.
    /// </summary>
    public ReadOnlyMemory<byte> Blocks { get; }
}
