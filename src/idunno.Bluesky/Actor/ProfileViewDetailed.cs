// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.AtProto.Repo;
using idunno.Bluesky.Graph;

namespace idunno.Bluesky.Actor
{
    /// <summary>
    /// A detailed view of an actor profile
    /// </summary>
    /// <remarks>
    /// <para>See <see href="https://github.com/bluesky-social/atproto/blob/main/lexicons/app/bsky/actor/defs.json" /> for the definition.</para>
    /// </remarks>
    public sealed record ProfileViewDetailed : ProfileView
    {
        /// <summary>
        /// Creates a new instance of <see cref="ProfileView"/>
        /// </summary>
        /// <param name="did">The <see cref="Did"/> of the actor.</param>
        /// <param name="handle">The <see cref="Handle"/> of the actor.</param>
        /// <param name="displayName">The display name for the actor.</param>
        /// <param name="avatar">A <see cref="Uri"/> to the actor's avatar, if any.</param>
        /// <param name="associated">Properties associated with the actor.</param>
        /// <param name="viewer">Metadata about the current user's relationship to the actor.</param>
        /// <param name="description">The actor's description from their profile.</param>
        /// <param name="pronouns">The pronouns for the account, if any.</param>
        /// <param name="website">The website for the account, if any.</param>
        /// <param name="labels">Labels applied to the actor.</param>
        /// <param name="indexedAt">The date and time the actor was last indexed.</param>
        /// <param name="createdAt">The date and time the actor was created.</param>
        /// <param name="banner">A <see cref="Uri"/> to the actor's banner image.</param>
        /// <param name="verification">The <see cref="VerificationState"/> of the actor, if any.</param>
        /// <param name="followersCount">The actor's current follower count.</param>
        /// <param name="followsCount">The number of actors the actor follows.</param>
        /// <param name="postsCount">The number of posts the actor has made.</param>
        /// <param name="joinedViaStarterPack">A view over the start pack the user joined using, if any.</param>
        /// <param name="pinnedPost">A <see cref="StrongReference"/> to the actor's pinned post, if any.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="did"/> or <paramref name="handle"/> are null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        ///   Thrown when <paramref name="displayName"/> is not null and has a character length greater than 640 or a grapheme length greater than 64.
        /// </exception>
        [JsonConstructor]
        public ProfileViewDetailed(
            Did did,
            Handle handle,
            string? displayName,
            string? description,
            string? pronouns,
            Uri? website,
            Uri? avatar,
            Uri? banner,
            ProfileAssociated? associated,
            ActorViewerState? viewer,
            IReadOnlyCollection<Label>? labels,
            DateTimeOffset? indexedAt,
            DateTimeOffset? createdAt,
            VerificationState? verification,
            StarterPackViewBasic? joinedViaStarterPack = null,
            StrongReference? pinnedPost = null,
            int followersCount = 0,
            int followsCount = 0,
            int postsCount = 0
            ) : base(did, handle, displayName, description, pronouns, website, avatar, associated, viewer, labels, indexedAt, createdAt, verification)
        {
            Banner = banner;
            FollowersCount = followersCount;
            FollowsCount = followsCount;
            PostsCount = postsCount;
            JoinedViaStarterPack = joinedViaStarterPack;
            PinnedPost = pinnedPost;
        }

        /// <summary>
        /// Gets the <see cref="Uri"/> to the actor's banner image, if any.
        /// </summary>
        public Uri? Banner { get; init; }

        /// <summary>
        /// Gets the actor's current follower count.
        /// </summary>
        public int FollowersCount { get; init; }

        /// <summary>
        /// The number of actors the actor follows.
        /// </summary>
        public int FollowsCount { get; init; }

        /// <summary>
        /// The number of posts the actor has made.
        /// </summary>
        public int PostsCount { get; init; }

        /// <summary>
        /// A <see cref="StrongReference"/> to the actor's pinned post, if any.
        /// </summary>
        public StrongReference? PinnedPost { get; init; }

        /// <summary>
        /// A view over the start pack the user joined using, if any.
        /// </summary>
        public StarterPackViewBasic? JoinedViaStarterPack { get; init; }

        /// <summary>
        /// Gets a string representation of the <see cref="ProfileView"/>.
        /// This returns the actor's display name, if any, otherwise returns the actor's handle.
        /// </summary>
        public override string ToString()
        {
            if (!string.IsNullOrWhiteSpace(DisplayName))
            {
                return $"{DisplayName} ({Handle})";
            }
            else
            {
                return $"{Handle}";
            }
        }
    }
}
