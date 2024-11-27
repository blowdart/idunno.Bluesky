// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.AtProto.Repo;
using idunno.Bluesky.Actor;
using idunno.Bluesky.RichText;

namespace idunno.Bluesky.Feed
{
    /// <summary>
    /// A vew over a feed generator.
    /// </summary>
    /// <remarks>
    ///<para>See https://github.com/bluesky-social/atproto/blob/main/lexicons/app/bsky/feed/defs.json for definition.</para>
    /// </remarks>
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public sealed record GeneratorView : View
    {
        [JsonConstructor]
        internal GeneratorView(
            AtUri uri,
            Cid cid,
            Did did,
            ProfileView creator,
            string displayName,
            string? description,
            IReadOnlyList<Facet> facets,
            Uri? avatarUri,
            int? likeCount,
            bool? acceptsInteractions,
            IReadOnlyCollection<Label>? labels,
            GeneratorViewerState? viewer,
            DateTimeOffset indexedAt)
        {
            ArgumentNullException.ThrowIfNull(uri);
            ArgumentNullException.ThrowIfNull(cid);
            ArgumentNullException.ThrowIfNull(did);
            ArgumentNullException.ThrowIfNull(creator);
            ArgumentNullException.ThrowIfNullOrEmpty(displayName);

            Uri = uri;
            Cid = cid;

            Did = did;
            Creator = creator;
            DisplayName = displayName;
            Description = description;

            if (facets is not null)
            {
                Facets = facets;
            }
            else
            {
                Facets = new List<Facet>().AsReadOnly();
            }

            AvatarUri = avatarUri;

            if (likeCount is not null)
            {
                LikeCount = likeCount;
            }
            else
            {
                LikeCount = 0;
            }

            if (acceptsInteractions is not null)
            {
                AcceptsInteractions = acceptsInteractions;
            }
            else
            {
                AcceptsInteractions = false;
            }

            if (labels is not null)
            {
                Labels = labels;
            }
            else
            {
                Labels = new List<Label>().AsReadOnly();
            }

            Viewer = viewer;

            IndexedAt = indexedAt;
        }

        /// <summary>
        /// Gets the <see cref="AtUri"/> of the record.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public AtUri Uri { get; init; }

        /// <summary>
        /// Gets the <see cref="AtProto.Cid">Content Identifier</see> of the record.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public Cid Cid { get; init; }

        /// <summary>
        /// Gets a <see cref="StrongReference"/> for the record.
        /// </summary>
        [JsonIgnore]
        public StrongReference StrongReference => new(Uri, Cid);

        /// <summary>
        /// Gets the <see cref="Did"/> of the generator.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public Did Did { get; init; }

        /// <summary>
        /// Gets a <see cref="ProfileView"/> of the creator.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public ProfileView Creator { get; init; }

        /// <summary>
        /// Gets the display name of the generator.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public string DisplayName { get; init; }

        /// <summary>
        /// Gets the generator's description.
        /// </summary>
        [JsonInclude]
        public string? Description { get; init; }

        /// <summary>
        /// Gets any facets to apply to the generator description.
        /// </summary>
        [JsonInclude]
        [NotNull]
        public IReadOnlyList<Facet>? Facets { get; init; }

        /// <summary>
        /// Gets a <see cref="Uri"/> to an image to use as the generator's avatar.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("avatar")]
        public Uri? AvatarUri { get; init; }

        /// <summary>
        /// Gets a count of the number of likes the generator has.
        /// </summary>
        [JsonInclude]
        [NotNull]
        public int? LikeCount { get; init; }

        /// <summary>
        /// Gets a flag indicating whether the generator supports and accepts interactions.
        /// </summary>
        [JsonInclude]
        [NotNull]
        public bool? AcceptsInteractions { get; init; }

        /// <summary>
        /// Gets a collection of labels applied to the generator, if any.
        /// </summary>
        [JsonInclude]
        [NotNull]
        public IReadOnlyCollection<Label>? Labels { get; init; }

        /// <summary>
        /// Gets a <see cref="GeneratorViewerState"/> showing the relationship between the current user and the generator, if any.
        /// </summary>
        [JsonInclude]
        public GeneratorViewerState? Viewer { get; init; }

        /// <summary>
        /// Gets the <see cref="DateTimeOffset"/> the generator was indexed on.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public DateTimeOffset IndexedAt { get; init; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay
        {
            get
            {
                if (!string.IsNullOrEmpty(Description))
                {
                    return '{' + $"{DisplayName} : {Description} ({Uri})" + "}";
                }
                else
                {
                    return '{' + $"{DisplayName} ({Uri}" + "}";
                }
            }
        }
    }
}
