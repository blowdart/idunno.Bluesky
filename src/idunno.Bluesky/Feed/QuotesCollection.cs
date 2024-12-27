// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.AtProto.Repo;

namespace idunno.Bluesky.Feed
{
    /// <summary>
    /// A paged read-only collection of posts quoting an individual post.
    /// </summary>
    public sealed class QuotesCollection : PagedViewReadOnlyCollection<PostView>
    {
        internal QuotesCollection(AtUri uri, Cid? cid, IList<PostView> posts, string? cursor = null) : base(posts, cursor)
        {
            Uri = uri;
            Cid = cid;
        }

        internal QuotesCollection(AtUri uri, Cid? cid, IEnumerable<PostView> posts, string? cursor = null) : this(uri, cid, new List<PostView>(posts), cursor)
        {
        }

        internal QuotesCollection(AtUri uri, Cid? cid = null, string? cursor = null) : this(uri, cid, new List<PostView>(), cursor)
        {
        }

        /// <summary>
        /// Gets the <see cref="AtUri"/> of the post being quoted.
        /// </summary>
        public AtUri Uri { get; init; }

        /// <summary>
        /// Gets the <see cref="AtProto.Cid"/> of the post being quoted, if any.
        /// </summary>
        public Cid? Cid { get; init; }

        /// <summary>
        /// Gets the <see cref="StrongReference"/> to the post being quoted, if any.
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
