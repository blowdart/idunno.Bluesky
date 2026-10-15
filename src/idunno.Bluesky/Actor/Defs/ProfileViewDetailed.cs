// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.AtProto.Repo;
using idunno.Bluesky.Graph;

namespace idunno.Bluesky.Actor;

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
    /// <param name="description">The actor's description from their profile.</param>
    /// <param name="pronouns">The pronouns for the account, if any.</param>
    /// <param name="website">The website for the account, if any.</param>
    /// <param name="avatar">A <see cref="Uri"/> to the actor's avatar, if any.</param>
    /// <param name="banner">A <see cref="Uri"/> to the actor's banner image.</param>
    /// <param name="associated">Properties associated with the actor.</param>
    /// <param name="viewer">Metadata about the current user's relationship to the actor.</param>
    /// <param name="labels">Labels applied to the actor.</param>
    /// <param name="indexedAt">The date and time the actor was last indexed.</param>
    /// <param name="createdAt">The date and time the actor was created.</param>
    /// <param name="verification">The <see cref="VerificationState"/> of the actor, if any.</param>
    /// <param name="status">The <see cref="StatusView"/> of the actor, if any.</param>
    /// <param name="joinedViaStarterPack">A view over the starter pack the user joined using, if any.</param>
    /// <param name="pinnedPost">A <see cref="StrongReference"/> to the actor's pinned post, if any.</param>
    /// <param name="followersCount">The actor's current follower count.</param>
    /// <param name="followsCount">The number of actors the actor follows.</param>
    /// <param name="postsCount">The number of posts the actor has made.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="did"/> or <paramref name="handle"/> are <see langword="null"/>.</exception>
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
        ViewerState? viewer,
        IReadOnlyCollection<Label>? labels,
        DateTimeOffset? indexedAt,
        DateTimeOffset? createdAt,
        VerificationState? verification,
        StatusView? status,
        StarterPackViewBasic? joinedViaStarterPack = null,
        StrongReference? pinnedPost = null,
        int followersCount = 0,
        int followsCount = 0,
        int postsCount = 0) : this(
            did,
            handle,
            displayName,
            description,
            pronouns,
            website,
            avatar,
            banner,
            associated,
            viewer,
            labels,
            indexedAt,
            createdAt,
            verification,
            status,
            joinedViaStarterPack,
            pinnedPost,
            followersCount,
            followsCount,
            postsCount,
            links: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileViewDetailed"/> class.
    /// </summary>
    /// <param name="did">The <see cref="Did"/> of the actor.</param>
    /// <param name="handle">The <see cref="Handle"/> of the actor.</param>
    /// <param name="displayName">The display name for the actor.</param>
    /// <param name="description">The actor's description from their profile.</param>
    /// <param name="pronouns">The pronouns for the account, if any.</param>
    /// <param name="website">The website for the account, if any.</param>
    /// <param name="avatar">A <see cref="Uri"/> to the actor's avatar, if any.</param>
    /// <param name="banner">A <see cref="Uri"/> to the actor's banner image.</param>
    /// <param name="associated">Properties associated with the actor.</param>
    /// <param name="viewer">Metadata about the current user's relationship to the actor.</param>
    /// <param name="labels">Labels applied to the actor.</param>
    /// <param name="indexedAt">The date and time the actor was last indexed.</param>
    /// <param name="createdAt">The date and time the actor was created.</param>
    /// <param name="verification">The <see cref="VerificationState"/> of the actor, if any.</param>
    /// <param name="status">The <see cref="StatusView"/> of the actor, if any.</param>
    /// <param name="joinedViaStarterPack">A view over the starter pack the user joined using, if any.</param>
    /// <param name="pinnedPost">A <see cref="StrongReference"/> to the actor's pinned post, if any.</param>
    /// <param name="followersCount">The actor's current follower count.</param>
    /// <param name="followsCount">The number of actors the actor follows.</param>
    /// <param name="postsCount">The number of posts the actor has made.</param>
    /// <param name="links">The actor's profile links, if any.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="did"/> or <paramref name="handle"/> are <see langword="null"/>.</exception>
    [JsonConstructor]
    internal ProfileViewDetailed(
        Did did,
        Handle handle,
        string? displayName,
        string? description,
        string? pronouns,
        Uri? website,
        Uri? avatar,
        Uri? banner,
        ProfileAssociated? associated,
        ViewerState? viewer,
        IReadOnlyCollection<Label>? labels,
        DateTimeOffset? indexedAt,
        DateTimeOffset? createdAt,
        VerificationState? verification,
        StatusView? status,
        StarterPackViewBasic? joinedViaStarterPack,
        StrongReference? pinnedPost,
        int followersCount,
        int followsCount,
        int postsCount,
        IReadOnlyCollection<ProfileLinkView>? links) : base(did, handle, displayName, description, pronouns, website, avatar, associated, viewer, labels, indexedAt, createdAt, verification, status)
    {
        Banner = banner;
        FollowersCount = followersCount;
        FollowsCount = followsCount;
        PostsCount = postsCount;
        JoinedViaStarterPack = joinedViaStarterPack;
        PinnedPost = pinnedPost;
        Links = links;
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
    /// A view over the starter pack the user joined using, if any.
    /// </summary>
    public StarterPackViewBasic? JoinedViaStarterPack { get; init; }

    /// <summary>
    /// Gets the actor's profile links, if any.
    /// </summary>
    /// <exception cref="ArgumentNullException">An entry is <see langword="null"/>.</exception>
    public IReadOnlyCollection<ProfileLinkView>? Links
    {
        get;
        init
        {
            if (value is not null)
            {
                foreach (ProfileLinkView link in value)
                {
                    ArgumentNullException.ThrowIfNull(link);
                }
            }

            field = value is null ? null : Array.AsReadOnly(value.ToArray());
        }
    }

    /// <summary>
    /// Gets a string representation of the <see cref="ProfileView"/>.
    /// This returns the actor's display name, if any, otherwise returns the actor's handle.
    /// </summary>
    /// <returns>A string representation of the <see cref="ProfileView"/>.</returns>
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