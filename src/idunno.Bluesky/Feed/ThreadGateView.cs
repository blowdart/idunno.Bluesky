// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.Bluesky.Feed.Gates;
using idunno.Bluesky.Graph;

namespace idunno.Bluesky.Feed
{
    /// <summary>
    /// Represents a thread gate, rules that set who can reply to a post.
    /// If a thread gate record is present but empty, then nobody can reply.
    /// </summary>
    /// <remarks>
    /// <para>See <see href="https://docs.bsky.app/docs/tutorials/thread-gates"/> for documentation.</para>
    /// </remarks>
    public record ThreadGateView
    {
        /// <summary>
        /// Creates a new instance of <see cref="ThreadGateView"/> with the specified parameters.
        /// </summary>
        /// <param name="uri">The <see cref="AtUri"/> of the thread gate.</param>
        /// <param name="cid">The <see cref="AtProto.Cid">content identifier</see> of the thread gate.</param>
        /// <param name="record">The record for the <see cref="ThreadGate"/>.</param>
        /// <param name="lists">A collection of rules for the thread gate.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> or <paramref name="cid"/> are <see langword="null"/>.</exception>
        [JsonConstructor]
        public ThreadGateView(
            AtUri uri,
            Cid cid,
            ThreadGate? record,
            IReadOnlyCollection<ListViewBasic>? lists)
        {
            ArgumentNullException.ThrowIfNull(uri);
            ArgumentNullException.ThrowIfNull(cid);

            Uri = uri;
            Cid = cid;

            if (lists is not null)
            {
                Lists = lists;
            }
            else
            {
                Lists = new List<ListViewBasic>().AsReadOnly<ListViewBasic>();
            }

            Record = record;
        }

        /// <summary>
        /// Gets the <see cref="AtUri"/> of the thread gate.
        /// </summary>
        public AtUri Uri { get; init; }

        /// <summary>
        /// Gets the The <see cref="AtProto.Cid">content identifier</see> of the thread gate.
        /// </summary>
        public Cid Cid { get; init; }

        /// <summary>
        /// Gets the record for the <see cref="ThreadGate"/>.
        /// </summary>
        public ThreadGate? Record { get; init; }

        /// <summary>
        /// Gets the collection of rules for the thread gate.
        /// </summary>
        public IReadOnlyCollection<ListViewBasic> Lists { get; init; }
    }
}
