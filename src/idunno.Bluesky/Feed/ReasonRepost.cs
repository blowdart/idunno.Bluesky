// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Actor;

namespace idunno.Bluesky.Feed
{
    /// <summary>
    /// Indicates a feed presence caused by a repost.
    /// </summary>
    public sealed record ReasonRepost : ReasonBase
    {
        /// <summary>
        /// Creates a new instance of <see cref="ReasonRepost"/>
        /// </summary>
        /// <param name="by">The <see cref="ProfileViewBasic"/> of the actor who reposted the post.</param>
        /// <param name="indexedAt">The <see cref="DateTimeOffset"/> when the repost was indexed.</param>
        /// <param name="uri">An optional <see cref="AtUri"/> of the post that was reposted.</param>
        /// <param name="cid">An optional <see cref="idunno.AtProto.Cid">content identifier</see> of the post that was reposted.</param>
        [JsonConstructor]
        internal ReasonRepost(ProfileViewBasic by, DateTimeOffset indexedAt, AtUri? uri, Cid? cid)
        {
            By = by;
            IndexedAt = indexedAt;
            Uri = uri;
            Cid = cid;

            if (uri is not null && cid is not null)
            {
                StrongReference = new StrongReference(uri, cid);
            }
        }

        /// <summary>
        /// Gets the <see cref="ProfileViewBasic"/> of the actor who reposted the post.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public ProfileViewBasic By { get; init; }

        /// <summary>
        /// Gets the <see cref="DateTimeOffset"/> when the repost was indexed.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public DateTimeOffset IndexedAt { get; init; }

        /// <summary>
        /// Gets the <see cref="AtUri" /> of the post that was reposted.
        /// </summary>
        [JsonInclude]
        public AtUri? Uri { get; init; }

        /// <summary>
        /// Gets the <see cref="Cid">content identifier</see> of the post that was reposted.
        /// </summary>
        [JsonInclude]
        public Cid? Cid { get; init; }

        /// <summary>
        /// Gets a strong reference to the repost record.
        /// </summary>
        [JsonIgnore]
        public StrongReference? StrongReference { get; }
    }
}
