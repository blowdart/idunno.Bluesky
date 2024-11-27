// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using idunno.AtProto.Repo;

namespace idunno.Bluesky.Actor
{
    /// <summary>
    /// A declaration of a Bluesky account profile.
    /// </summary>
    [SuppressMessage("Naming", "CA1724: Type names should not match namespaces", Justification = "System.Web.Profile has been long deprecated and cannot be used with ASP.NET Core, so no confusion should arise.")]
    public sealed record Profile : BlueskyRecord
    {
        /// <summary>
        /// Creates a new instance of <see cref="Profile"/>.
        /// </summary>
        /// <param name="displayName">The display name of the account, if any.</param>
        /// <param name="description">The description for the account, if any.</param>
        /// <param name="avatar">A small image to be displayed next to posts from account, if any.</param>
        /// <param name="banner">A larger horizontal image to display behind profile view, if any.</param>
        /// <param name="joinedViaStarterPack">A <see cref="StrongReference"/> to the starter pack the account joined through, if any.</param>
        /// <param name="pinnedPost">A <see cref="StrongReference"/> to the profile's pinned post, if any.</param>
        /// <param name="createdAt">The <see cref="DateTimeOffset"/> the account was created.</param>
        [JsonConstructor]
        public Profile(
            string? displayName,
            string? description,
            Blob? avatar,
            Blob? banner,
            StrongReference? joinedViaStarterPack,
            StrongReference? pinnedPost,
            DateTimeOffset createdAt) : base(createdAt)
        {
            DisplayName = displayName;
            Description = description;
            Avatar = avatar;
            Banner = banner;
            JoinedViaStarterPack = joinedViaStarterPack;
            PinnedPost = pinnedPost;
        }

        /// <summary>
        /// Gets the display name of the account.
        /// </summary>
        [JsonInclude]
        public string? DisplayName { get; init; }

        /// <summary>
        /// Gets the description for the account.
        /// </summary>
        [JsonInclude]
        public string? Description { get; init; }

        /// <summary>
        /// Gets a small image to be displayed next to posts from account.
        /// </summary>
        [JsonInclude]
        public Blob? Avatar { get; init; }

        /// <summary>
        /// Gets a larger horizontal image to display behind profile view
        /// </summary>
        [JsonInclude]
        public Blob? Banner { get; init; }

        /// <summary>
        /// If the user joined via a starter pack gets a <see cref="StrongReference"/> to that starter pack.
        /// </summary>
        [JsonInclude]
        public StrongReference? JoinedViaStarterPack { get; init; }

        /// <summary>
        /// Gets a <see cref="StrongReference"/> to the account's pinned post, if any.
        /// </summary>
        [JsonInclude]
        public StrongReference? PinnedPost { get; init; }
    }
}
