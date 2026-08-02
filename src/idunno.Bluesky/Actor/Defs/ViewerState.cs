// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.Bluesky.Graph;
using idunno.Bluesky.Notifications;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Actor;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Metadata about the requesting account's relationship with the subject account. Only has meaningful content for authenticated requests
/// </summary>
/// <remarks>
///<para>See https://github.com/bluesky-social/atproto/blob/main/lexicons/app/bsky/actor/defs.json for definition.</para>
/// </remarks>
public record ViewerState
{
    /// <summary>
    /// Creates a new instance of <see cref="ViewerState"/>
    /// </summary>
    /// <param name="muted">Flag indicating whether the actor is muted by the current user.</param>
    /// <param name="mutedOnlyReposts">Flag indicating whether the actor's reposts are muted by the current user.</param>
    /// <param name="mutedOnlyQuotePosts">Flag indicating whether the actor's quoteposts are muted by the current user.</param>
    /// <param name="mutedByList">A <see cref="ListViewBasic"/> of the list the current user subscribes to which has muted the actor, if any.</param>
    /// <param name="blockedBy">Flag indicating whether the actor is blocked by the current user.</param>
    /// <param name="blocking">An <see cref="AtUri"/> reference to the block record of the actor, if they are blocking the current user.</param>
    /// <param name="blockingByList">A <see cref="ListViewBasic"/> of the list the current user subscribes to which has muted the actor, if any.</param>
    /// <param name="following">An <see cref="AtUri"/> reference to the follow record, if the current user is following the actor.</param>
    /// <param name="followedBy">An <see cref="AtUri"/> reference to the actor's follow record, if the the actor is following the current user.</param>
    /// <param name="knownFollowers">A <see cref="KnownFollowers"/> record of mutual followers shared between the actor and the current user, if any.</param>
    /// <param name="activitySubscription">Any <see cref="ActivitySubscription" /> the current user has to the subject's activity.</param>
    [JsonConstructor]
    internal ViewerState(
        bool? muted,
        bool? mutedOnlyReposts,
        bool? mutedOnlyQuotePosts,
        ListViewBasic? mutedByList,
        bool? blockedBy,
        AtUri? blocking,
        ListViewBasic? blockingByList,
        AtUri? following,
        AtUri? followedBy,
        KnownFollowers? knownFollowers,
        ActivitySubscription? activitySubscription)
    {
        Muted = muted;
        MutedOnlyReposts = mutedOnlyReposts;
        MutedOnlyQuotePosts = mutedOnlyQuotePosts;
        MutedByList = mutedByList;

        BlockedBy = blockedBy;
        Blocking = blocking;
        BlockingByList = blockingByList;

        Following = following;
        FollowedBy = followedBy;

        KnownFollowers = knownFollowers;

        ActivitySubscription = activitySubscription;

        if (muted is null)
        {
            Muted = false;
        }

        if (mutedOnlyReposts is null)
        {
            MutedOnlyReposts = false;
        }

        if (mutedOnlyQuotePosts is null)
        {
            MutedOnlyQuotePosts = false;
        }

        if (blockedBy is null)
        {
            BlockedBy = false;
        }
    }

    /// <summary>
    /// Gets a flag indicating the account is fully muted, directly or via a mutelist. <see langword="false" /> when the mute is scoped to specific kinds; see mutedOnlyReposts and mutedOnlyQuoteposts.
    /// </summary>
    [NotNull]
    public bool? Muted { get; init; }

    /// <summary>
    /// Gets a flag indicating whether the account's reposts are muted. Scoped mutes are exclusive with muted: this can be <see langword="true" /> while muted is <see langword="false" />.
    /// If muted is <see langword="true" />, this will be <see langword="false" />.
    /// </summary>
    [NotNull]
    public bool? MutedOnlyReposts { get; init; }

    /// <summary>
    /// Gets a flag indicating whether the account's quoteposts are muted. Scoped mutes are exclusive with muted: this can be <see langword="true" /> while muted is <see langword="false" />.
    /// If muted is <see langword="true" />, this will be <see langword="false" />.
    /// </summary>
    [NotNull]
    [JsonPropertyName("mutedOnlyQuoteposts")]
    public bool? MutedOnlyQuotePosts { get; init; }

    /// <summary>
    /// Gets a <see cref="ListViewBasic"/> of the list the current user subscribes to which has muted the actor, if any.
    /// </summary>
    public ListViewBasic? MutedByList { get; init; }

    /// <summary>
    /// Gets a flag indicating whether the actor is blocked by the current user.
    /// </summary>
    [NotNull]
    public bool? BlockedBy { get; init; }

    /// <summary>
    /// Gets an <see cref="AtUri"/> reference to the block record of the actor, if they are blocking the current user.
    /// </summary>
    public AtUri? Blocking { get; init; }

    /// <summary>
    /// Gets a <see cref="ListViewBasic"/> of the list the current user subscribes to which has muted the actor, if any.
    /// </summary>
    public ListViewBasic? BlockingByList { get; init; }

    /// <summary>
    /// Gets an <see cref="AtUri"/> reference to the follow record, if the current user is following the actor.
    /// </summary>
    public AtUri? Following { get; init; }

    /// <summary>
    /// Gets an <see cref="AtUri"/> reference to the actor's follow record, if the the actor is following the current user
    /// </summary>
    public AtUri? FollowedBy { get; init; }

    /// <summary>
    /// Gets a <see cref="KnownFollowers"/> record of mutual followers shared between the actor and the current user, if any.
    /// </summary>
    /// <remarks>
    ///<para>This property is present only in selected cases, as an optimization.</para>
    /// </remarks>
    public KnownFollowers? KnownFollowers { get; init; }

    /// <summary>
    /// Gets <see cref="ActivitySubscription"/> the current user has to the subject's activity, if any.
    /// </summary>
    /// <remarks>
    ///<para>This property is present only in selected cases, as an optimization.</para>
    /// </remarks>
    public ActivitySubscription? ActivitySubscription { get; init; }
}