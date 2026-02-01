// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.Bluesky.Actor;
using idunno.Bluesky.RichText;

namespace idunno.Bluesky.Graph
{
    /// <summary>
    /// Encapsulates a view over a list.
    /// </summary>
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public sealed record ListView : ListViewBasic
    {
        /// <summary>
        /// Creates a new instance of <see cref="ListViewBasic"/>
        /// </summary>
        /// <param name="uri">The <see cref="AtUri"/> of the list.</param>
        /// <param name="cid">The <see cref="Cid">content identifier</see> of the list.</param>
        /// <param name="creator">A <see cref="ProfileView"/> of the list creator.</param>
        /// <param name="name">The name of the list.</param>
        /// <param name="purpose">The list's purpose.</param>
        /// <param name="description">The description of the list.</param>
        /// <param name="descriptionFacets">Any <see cref="Facet"/>s applied to the <paramref name="description"/>.</param>
        /// <param name="avatar">A <see cref="Uri"/> to the list's avatar, if any.</param>
        /// <param name="listItemCount">The number of items in the list.</param>
        /// <param name="labels">Labels applied to the list</param>
        /// <param name="viewer">A view of the relationship between the view and the current user.</param>
        /// <param name="indexedAt">The date and time the list was last indexed at.</param>
        [JsonConstructor]
        public ListView(
            AtUri uri,
            Cid cid,
            ProfileView creator,
            string name,
            ListPurpose purpose,
            string? description,
            IReadOnlyCollection<Facet>? descriptionFacets,
            Uri? avatar,
            int? listItemCount,
            IReadOnlyCollection<Label>? labels,
            ListViewerState? viewer,
            DateTimeOffset? indexedAt) : base(uri, cid, name, purpose, avatar, listItemCount, labels, viewer, indexedAt)
        {
            Creator = creator;
            Description = description;
            DescriptionFacets = descriptionFacets;
        }

        /// <summary>
        /// Gets a <see cref="ProfileView"/> of the list creator.
        /// </summary>
        [JsonRequired]
        public ProfileView Creator { get; init; }

        /// <summary>
        /// Gets the description of the list.
        /// </summary>
        public string? Description { get; init; }

        /// <summary>
        /// Gets any <see cref="Facet" />s applied to the <see cref="Description"/>.
        /// </summary>
        public IReadOnlyCollection<Facet>? DescriptionFacets { get; init; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay
        {
            get
            {
                if (!string.IsNullOrEmpty(Description))
                {
                    return '{' + $"{Name} : {Description} ({Uri})" + "}";
                }
                else
                {
                    return '{' + $"{Name} ({Uri}" + "}";
                }
            }
        }
    }
}
