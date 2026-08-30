// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.AtProto.Repo;

namespace idunno.Bluesky.Feed;

/// <summary>
/// A <see cref="PagedReadOnlyCollection{T}"/> of <see cref="LikesLike"/>s for a post.
/// </summary>
public sealed class Likes : PagedReadOnlyCollection<LikesLike>
{
    internal Likes(AtUri uri, Cid? cid, IList<LikesLike> list, string? cursor = null) : base(list, cursor)
    {
        Uri = uri;
        Cid = cid;
    }

    internal Likes(AtUri uri, Cid? cid, IEnumerable<LikesLike> collection, string? cursor = null) : this(uri, cid, [.. collection], cursor)
    {
    }

    internal Likes(AtUri uri, Cid? cid = null, string? cursor = null) : this(uri, cid, [], cursor)
    {
    }

    /// <summary>
    /// The <see cref="AtUri"/> of the post whose likes are listed.
    /// </summary>
    public AtUri Uri { get; init; }

    /// <summary>
    /// An optional <see cref="Cid">content identifier</see> of the post whose likes are listed.
    /// </summary>
    public Cid? Cid { get; init; }

    /// <summary>
    /// A <see cref="StrongReference"/> to the post whose likes are listed, if any.
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