// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.Bluesky.Graph;
using idunno.Bluesky.Notifications;

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
        /// <summary>
        /// Creates a new instance of <see cref="ActorViewerState"/>
        /// </summary>
        /// <param name="muted">Flag indicating whether the actor is muted by the current user.</param>
        /// <param name="mutedByList">A <see cref="ListViewBasic"/> of the list the current user subscribes to which has muted the actor, if any.</param>
        /// <param name="blockedBy">Flag indicating whether the actor is blocked by the current user.</param>
        /// <param name="blocking">An <see cref="AtUri"/> reference to the block record of the actor, if they are blocking the current user.</param>
        /// <param name="blockingByList">A <see cref="ListViewBasic"/> of the list the current user subscribes to which has muted the actor, if any.</param>
        /// <param name="following">An <see cref="AtUri"/> reference to the follow record, if the current user is following the actor.</param>
        /// <param name="followedBy">An <see cref="AtUri"/> reference to the actor's follow record, if the the actor is following the current user.</param>
        /// <param name="knownFollowers">A <see cref="KnownFollowers"/> record of mutual followers shared between the actor and the current user, if any.</param>
        /// <param name="activitySubscription">Any <see cref="ActivitySubscription" /> the current user has to the subject's activity.</param>
        [JsonConstructor]
        public ActorViewerState(
            bool muted,
            ListViewBasic? mutedByList,
            bool blockedBy,
            AtUri? blocking,
            ListViewBasic? blockingByList,
            AtUri? following,
            AtUri? followedBy,
            KnownFollowers? knownFollowers,
            ActivitySubscription? activitySubscription)
        {
            Muted = muted;
            MutedByList = mutedByList;

            BlockedBy = blockedBy;
            Blocking = blocking;
            BlockingByList = blockingByList;

            Following = following;
            FollowedBy = followedBy;

            KnownFollowers = knownFollowers;

            ActivitySubscription = activitySubscription;
        }

        /// <summary>
        /// Gets a flag indicating the subject account has been muted by the requesting account.
        /// </summary>
        [JsonInclude]
        public bool Muted { get; init; }

        /// <summary>
        /// Gets a <see cref="ListViewBasic"/> of the list the current user subscribes to which has muted the actor, if any.
        /// </summary>
        [JsonInclude]
        public ListViewBasic? MutedByList { get; init; }

        /// <summary>
        /// Gets a flag indicating whether the actor is blocked by the current user.
        /// </summary>
        [JsonInclude]
        public bool BlockedBy { get; init; }

        /// <summary>
        /// Gets an <see cref="AtUri"/> reference to the block record of the actor, if they are blocking the current user.
        /// </summary>
        [JsonInclude]
        public AtUri? Blocking { get; init; }

        /// <summary>
        /// Gets a <see cref="ListViewBasic"/> of the list the current user subscribes to which has muted the actor, if any.
        /// </summary>
        [JsonInclude]
        public ListViewBasic? BlockingByList { get; init; }

        /// <summary>
        /// Gets an <see cref="AtUri"/> reference to the follow record, if the current user is following the actor.
        /// </summary>
        [JsonInclude]
        public AtUri? Following { get; init; }

        /// <summary>
        /// Gets an <see cref="AtUri"/> reference to the actor's follow record, if the the actor is following the current user
        /// </summary>
        [JsonInclude]
        public AtUri? FollowedBy { get; init; }

        /// <summary>
        /// Gets a <see cref="KnownFollowers"/> record of mutual followers shared between the actor and the current user, if any.
        /// </summary>
        /// <remarks>
        ///<para>This property is present only in selected cases, as an optimization.</para>
        /// </remarks>
        [JsonInclude]
        public KnownFollowers? KnownFollowers { get; init; }

        /// <summary>
        /// Gets <see cref="ActivitySubscription"/> the current user has to the subject's activity, if any.
        /// </summary>
        /// <remarks>
        ///<para>This property is present only in selected cases, as an optimization.</para>
        /// </remarks>
        [JsonInclude]
        public ActivitySubscription? ActivitySubscription { get; init; }
    }
}
