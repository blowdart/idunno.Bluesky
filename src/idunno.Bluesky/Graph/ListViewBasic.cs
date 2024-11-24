// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.AtProto.Repo;

namespace idunno.Bluesky.Graph
{
    //TODO: Missing documentation in lexicon.

    /// <summary>
    /// A basic view over a list.
    /// </summary>
    /// <remarks><para>See https://github.com/bluesky-social/atproto/blob/main/lexicons/app/bsky/graph/defs.json</para></remarks>
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public record ListViewBasic : View
    {
        /// <summary>
        /// Creates a new instance of <see cref="ListViewBasic"/>
        /// </summary>
        /// <param name="uri">The <see cref="AtUri"/> of the list.</param>
        /// <param name="cid">The <see cref="Cid">content identifier</see> of the list.</param>
        /// <param name="name">The name of the list.</param>
        /// <param name="purpose">The list's purpose.</param>
        /// <param name="avatar">A <see cref="Uri"/> to the list's avatar, if any.</param>
        /// <param name="listItemCount">The number of items in the list.</param>
        /// <param name="labels">Labels applied to the list</param>
        /// <param name="viewer"></param>
        /// <param name="indexedAt">The date and time the list was last indexed at.</param>
        /// <exception cref="ArgumentNullException">Throws if <paramref name="uri"/> or <paramref name="cid"/> is null.</exception>
        [JsonConstructor]
        public ListViewBasic(
            AtUri uri,
            Cid cid,
            string name,
            ListPurpose purpose,
            Uri? avatar,
            int? listItemCount,
            IReadOnlyCollection<Label>? labels,
            ListViewerState? viewer,
            DateTimeOffset? indexedAt)
        {
            ArgumentNullException.ThrowIfNull(uri);
            ArgumentNullException.ThrowIfNull(cid);
            ArgumentNullException.ThrowIfNullOrWhiteSpace(name);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(name.Length, 64);

            Uri = uri;
            Cid = cid;

            Name = name;
            Purpose = purpose;
            Avatar = avatar;

            if (listItemCount is not null)
            {
                ListItemCount = listItemCount;
            }
            else
            {
                ListItemCount = 0;
            }

            if (labels is null)
            {
                Labels = new List<Label>().AsReadOnly();
            }
            else
            {
                Labels = new List<Label>(labels).AsReadOnly();
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
        /// Gets the the name of the list.
        /// </summary>
        [JsonRequired]
        public string Name { get; init; }

        /// <summary>
        /// Gets the list's purpose.
        /// </summary>
        [JsonRequired]
        public ListPurpose Purpose { get; init; }

        /// <summary>
        /// A <see cref="Uri"/> to the list's avatar, if any.
        /// </summary>
        public Uri? Avatar { get; init; }

        /// <summary>
        /// The number of items in the list.
        /// </summary>
        [NotNull]
        public int? ListItemCount { get; init; }

        /// <summary>
        /// Labels applied to the list by the user's list subscriptions.
        /// </summary>
        [NotNull]
        public IReadOnlyCollection<Label> Labels { get; init; }

        public ListViewerState? Viewer { get; init; }

        /// <summary>
        /// The date and time the list was last indexed at.
        /// </summary>
        public DateTimeOffset? IndexedAt { get; init; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay
        {
            get
            {
                return $"{{{Name} ({Uri})}}";
            }
        }
    }
}
