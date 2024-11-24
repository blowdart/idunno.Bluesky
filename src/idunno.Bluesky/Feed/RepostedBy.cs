// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Repo;
using idunno.AtProto;
using idunno.Bluesky.Actor;

namespace idunno.Bluesky.Feed
{
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
