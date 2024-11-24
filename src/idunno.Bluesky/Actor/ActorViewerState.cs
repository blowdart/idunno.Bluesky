// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using idunno.AtProto;
using idunno.Bluesky.Graph;

namespace idunno.Bluesky.Actor
{
    /// <summary>
    /// Metadata about the requesting account's relationship with the subject account. Only has meaningful content for authenticated requests
    /// </summary>
    /// <remarks>
    ///<para>See https://github.com/bluesky-social/atproto/blob/main/lexicons/app/bsky/actor/defs.json for definition.</para>
    /// </remarks>
    public record ActorViewerState
    {
        [JsonConstructor]
        public ActorViewerState(
            bool muted,
            ListViewBasic? mutedByList,
            bool blockedBy,
            AtUri blocking,
            ListViewBasic? blockingByList,
            AtUri? following,
            AtUri? followedBy,
            KnownFollowers knownFollowers)
        {
            Muted = muted;
            MutedByList = mutedByList;

            BlockedBy = blockedBy;
            Blocking = blocking;
            BlockingByList = blockingByList;

            Following = following;
            FollowedBy = followedBy;

            KnownFollowers = knownFollowers;
        }

        /// <summary>
        /// Flag indicating the subject account has been muted by the requesting account.
        /// </summary>
        [JsonInclude]
        public bool Muted { get; init; }

        [JsonInclude]
        public ListViewBasic? MutedByList { get; init; }

        [JsonInclude]
        public bool BlockedBy { get; init; }

        [JsonInclude]
        public AtUri Blocking { get; init; }

        [JsonInclude]
        public ListViewBasic? BlockingByList { get; init; }

        [JsonInclude]
        public AtUri? Following { get; init; }

        [JsonInclude]
        public AtUri? FollowedBy { get; init; }

        [JsonInclude]
        public KnownFollowers KnownFollowers { get; init; }
    }
}
