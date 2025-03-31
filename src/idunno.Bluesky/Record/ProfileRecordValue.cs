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
    public sealed record ProfileRecordValue : BlueskyRecordValue
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private const string DiscourageLoggedOutUserLabelName = "!no-unauthenticated";

        /// <summary>
        /// Creates a new instance of <see cref="ProfileRecordValue"/>.
        /// </summary>
        /// <param name="displayName">The display name of the account, if any.</param>
        /// <param name="description">The description for the account, if any.</param>
        /// <param name="avatar">A small image to be displayed next to posts from account, if any.</param>
        /// <param name="banner">A larger horizontal image to display behind profile view, if any.</param>
        /// <param name="joinedViaStarterPack">A <see cref="StrongReference"/> to the starter pack the account joined through, if any.</param>
        /// <param name="pinnedPost">A <see cref="StrongReference"/> to the profile's pinned post, if any.</param>
        /// <param name="labels">Any <see cref="SelfLabels"/> applied to the profile.</param>
        /// <param name="createdAt">The <see cref="DateTimeOffset"/> the record was created on.</param>
        [JsonConstructor]
        public ProfileRecordValue(
            string? displayName,
            string? description,
            Blob? avatar,
            Blob? banner,
            StrongReference? joinedViaStarterPack,
            StrongReference? pinnedPost,
            SelfLabels? labels,
            DateTimeOffset? createdAt)
        {
            DisplayName = displayName;
            Description = description;
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
        /// Gets the json discriminator value for the record.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("$type")]
        [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Can't be static as it needs to be included in the json serialized value.")]
        public string Type => RecordType.Profile;

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
