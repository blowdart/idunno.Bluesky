// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.


using idunno.AtProto.Repo;
using idunno.AtProto;

namespace idunno.Bluesky.Feed
{
    public sealed class Quotes : PagedViewReadOnlyCollection<PostView>
    {
        internal Quotes(AtUri uri, Cid? cid, IList<PostView> posts, string? cursor = null) : base(posts, cursor)
        {
            Uri = uri;
            Cid = cid;
        }

        internal Quotes(AtUri uri, Cid? cid, IEnumerable<PostView> posts, string? cursor = null) : this(uri, cid, new List<PostView>(posts), cursor)
        {
        }

        internal Quotes(AtUri uri, Cid? cid = null, string? cursor = null) : this(uri, cid, new List<PostView>(), cursor)
        {
        }

        public AtUri Uri { get; init; }

        public Cid? Cid { get; init; }

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
