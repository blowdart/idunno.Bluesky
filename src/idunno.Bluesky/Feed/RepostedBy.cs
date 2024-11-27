// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Repo;
using idunno.AtProto;
using idunno.Bluesky.Actor;

namespace idunno.Bluesky.Feed
{
    /// <summary>
    /// A collection of <see cref="ProfileView"/>s returned from <see cref="BlueskyAgent.GetRepostedBy(AtUri, Cid?, int?, string?, IEnumerable{Did}?, CancellationToken)"/>.
    /// </summary>
    public sealed class RepostedBy : PagedViewReadOnlyCollection<ProfileView>
    {
        internal RepostedBy(AtUri uri, Cid? cid, IList<ProfileView> profileViews, string? cursor = null) : base(profileViews, cursor)
        {
            Uri = uri;
            Cid = cid;
        }

        internal RepostedBy(AtUri uri, Cid? cid, IEnumerable<ProfileView> profileViews, string? cursor = null) : this(uri, cid, new List<ProfileView>(profileViews), cursor)
        {
        }

        internal RepostedBy(AtUri uri, Cid? cid = null, string? cursor = null) : this(uri, cid, new List<ProfileView>(), cursor)
        {
        }

        /// <summary>
        /// Gets the <see cref="AtUri"/> of the post whose reposts are in the collection.
        /// </summary>
        public AtUri Uri { get; init; }

        /// <summary>
        /// Gets the <see cref="AtProto.Cid"/> of the post whose reposts are in the collection.
        /// </summary>
        public Cid? Cid { get; init; }

        /// <summary>
        /// Gets a <see cref="StrongReference"/> to the post whos reposts are in the collection.
        /// </summary>
        public StrongReference? StrongReference
        {
            get
            {
                if (Uri is not null && Cid is not null)
                {
                    return new StrongReference(Uri, Cid);
                }
                else
                {
                    return null;
                }
            }
        }
    }
}
