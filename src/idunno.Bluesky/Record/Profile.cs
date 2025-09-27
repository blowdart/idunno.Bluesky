// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using idunno.AtProto.Labels;
using idunno.AtProto.Repo;

namespace idunno.Bluesky.Record
{
    /// <summary>
    /// Encapsulates a Bluesky account profile.
    /// </summary>
    [SuppressMessage("Naming", "CA1724", Justification = "The System.Web Profile class is part of ASP.NET and has not been carried over to .NET")]
    public sealed record Profile : BlueskyRecord
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private const string DiscourageLoggedOutUserLabelName = "!no-unauthenticated";

        /// <summary>
        /// Creates a new instance of <see cref="Profile"/>.
        /// </summary>
        /// <param name="displayName">The display name of the account, if any.</param>
        /// <param name="description">The description for the account, if any.</param>
        /// <param name="pronouns">The pronouns for the account, if any.</param>
        /// <param name="website">The website for the account, if any.</param>
        /// <param name="avatar">A small image to be displayed next to posts from account, if any.</param>
        /// <param name="banner">A larger horizontal image to display behind profile view, if any.</param>
        /// <param name="pinnedPost">A <see cref="StrongReference"/> to the profile's pinned post, if any.</param>
        /// <param name="labels">Any <see cref="SelfLabels"/> applied to the profile.</param>
        /// <param name="createdAt">The <see cref="DateTimeOffset"/>The <see cref="DateTimeOffset"/> the record was created at.</param>
        public Profile(
            string? displayName = null,
            string? description = null,
            string? pronouns = null,
            Uri? website = null,
            Blob? avatar = null,
            Blob? banner = null,
            StrongReference? pinnedPost = null,
            SelfLabels? labels = null,
            DateTimeOffset? createdAt = null) : this(
                displayName: displayName,
                description: description,
                pronouns: pronouns,
                website: website,
                avatar: avatar,
                banner: banner,
                joinedViaStarterPack: null,
                pinnedPost: pinnedPost,
                labels: labels,
                createdAt: createdAt)
        {
            if (createdAt is not null)
            {
                CreatedAt = DateTimeOffset.UtcNow;
            }
        }

        /// <summary>
        /// Creates a new instance of <see cref="Profile"/>.
        /// </summary>
        /// <param name="displayName">The display name of the account, if any.</param>
        /// <param name="description">The description for the account, if any.</param>
        /// <param name="pronouns">The pronouns for the account, if any.</param>
        /// <param name="website">The website for the account, if any.</param>
        /// <param name="avatar">A small image to be displayed next to posts from account, if any.</param>
        /// <param name="banner">A larger horizontal image to display behind profile view, if any.</param>
        /// <param name="joinedViaStarterPack">A <see cref="StrongReference"/> to the starter pack the account joined through, if any.</param>
        /// <param name="pinnedPost">A <see cref="StrongReference"/> to the profile's pinned post, if any.</param>
        /// <param name="labels">Any <see cref="SelfLabels"/> applied to the profile.</param>
        /// <param name="createdAt">The <see cref="DateTimeOffset"/>The <see cref="DateTimeOffset"/> the record was created at.</param>
        [JsonConstructor]
        public Profile(
            string? displayName,
            string? description,
            string? pronouns,
            Uri? website,
            Blob? avatar,
            Blob? banner,
            StrongReference? joinedViaStarterPack,
            StrongReference? pinnedPost,
            SelfLabels? labels,
            DateTimeOffset? createdAt)
        {
            DisplayName = displayName;
            Description = description;
            Pronouns = pronouns;
            Website = website;
            Avatar = avatar;
            Banner = banner;
            JoinedViaStarterPack = joinedViaStarterPack;
            PinnedPost = pinnedPost;

            if (labels is not null)
            {
                Labels = labels;
            }
            else
            {
                Labels = new SelfLabels();
            }

            CreatedAt = createdAt;
        }

        /// <summary>
        /// Gets the creation date/time of the profile, if provided.
        /// </summary>
        public DateTimeOffset? CreatedAt { get; set; }

        /// <summary>
        /// Gets the display name of the account.
        /// </summary>
        [JsonInclude]
        public string? DisplayName { get; set; }

        /// <summary>
        /// Gets the description for the account.
        /// </summary>
        [JsonInclude]
        public string? Description { get; set; }

        /// <summary>
        /// Gets the pronouns for the account, if any.
        /// </summary>
        [JsonInclude]
        public string? Pronouns { get; set; }

        /// <summary>
        /// Gets the website for the account, if any.
        /// </summary>
        [JsonInclude]
        public Uri? Website { get; set; }

        /// <summary>
        /// Gets a small image to be displayed next to posts from account.
        /// </summary>
        [JsonInclude]
        public Blob? Avatar { get; set; }

        /// <summary>
        /// Gets a larger horizontal image to display behind profile view
        /// </summary>
        [JsonInclude]
        public Blob? Banner { get; set; }

        /// <summary>
        /// If the user joined via a starter pack gets a <see cref="StrongReference"/> to that starter pack.
        /// </summary>
        [JsonInclude]
        public StrongReference? JoinedViaStarterPack { get; init; }

        /// <summary>
        /// Gets a <see cref="StrongReference"/> to the account's pinned post, if any.
        /// </summary>
        [JsonInclude]
        public StrongReference? PinnedPost { get; set; }

        /// <summary>
        /// Gets any <see cref="SelfLabels"/> applied to the profile/
        /// </summary>
        /// <remarks>
        /// <para>Profile self labels can only be one of the known <see href="https://docs.bsky.app/docs/advanced-guides/moderation#global-label-values">global values</see>.</para>
        /// </remarks>
        [NotNull]
        [JsonInclude]
        public SelfLabels? Labels { get; init; }

        /// <summary>
        /// Gets or sets a flag indicating whether applications should be discouraged from showing this profile and this
        /// profile's posts to unauthenticated users.
        /// </summary>
        [JsonIgnore]
        public bool DiscourageShowingToLoggedOutUsers
        {
            get
            {
                return Labels.Contains(DiscourageLoggedOutUserLabelName);
            }

            set
            {
                if (value)
                {
                    Labels.AddLabel(DiscourageLoggedOutUserLabelName);
                }
                else
                {
                    Labels.RemoveLabel(DiscourageLoggedOutUserLabelName);
                }
            }
        }
    }
}
