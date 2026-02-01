// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.Bluesky.Feed;

namespace idunno.Bluesky.Record
{
    /// <summary>
    ///  Encapsulates a Bluesky starter pack.
    /// </summary>
    public record StarterPack : BlueskyTimestampedRecord
    {
        /// <summary>
        /// Creates a new instance of <see cref="StarterPack"/>.
        /// </summary>
        /// <param name="name">The name of the starter pack.</param>
        /// <param name="description">The description of the starter pack.</param>
        /// <param name="list">The <see cref="AtUri"/> of the starter pack.</param>
        /// <param name="feeds">A collection of <see cref="GeneratorView"/>s for any feeds in the starter pack.</param>
        /// <param name="createdAt">The <see cref="DateTimeOffset"/> the starter pack was created on.</param>
        /// <param name="updatedAt">The <see cref="DateTimeOffset"/> the starter pack was last updated.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if more than 3 feeds are provided in <paramref name="feeds"/>.</exception>
        [JsonConstructor]
        public StarterPack(
            string name,
            string? description,
            AtUri list,
            IReadOnlyList<FeedItem>? feeds,
            DateTimeOffset createdAt,
            DateTimeOffset? updatedAt) : base(createdAt)
        {
            Name = name;
            Description = description;
            List = list;

            if (feeds is not null)
            {
                ArgumentOutOfRangeException.ThrowIfGreaterThan(feeds.Count, 3);
            }
            Feeds = feeds;
            UpdatedAt = updatedAt;
        }

        /// <summary>
        /// Gets the name of the starter pack.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public string Name { get; set; }

        /// <summary>
        /// Gets the description of the starter pack
        /// </summary>
        [JsonInclude]
        public string? Description { get; init; }

        /// <summary>
        /// Gets the <see cref="AtUri"/> of the starter pack.
        /// </summary>
        [JsonInclude]
        public AtUri List { get; set; }

        /// <summary>
        /// Gets a collection of <see cref="GeneratorView"/>s for any feeds in the starter pack.
        /// </summary>
        [JsonInclude]
        public IReadOnlyList<FeedItem>? Feeds { get; init; }

        /// <summary>
        /// The <see cref="DateTimeOffset"/> the starter pack was last updated.
        /// </summary>
        [JsonInclude]
        public DateTimeOffset? UpdatedAt { get; init; }
    }

    /// <summary>
    /// Represents a reference to a feed.
    /// </summary>
    public record FeedItem
    {
        /// <summary>
        /// Creates a new instance of <see cref="FeedItem"/>.
        /// </summary>
        /// <param name="uri">The feed URI</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="uri"/> is <see langword="null"/>.</exception>.
        [JsonConstructor]
        public FeedItem(AtUri uri)
        {
            ArgumentNullException.ThrowIfNull(uri);
            Uri = uri;
        }

        /// <summary>
        /// The AT URI of the feed.
        /// </summary>
        [JsonRequired]
        public AtUri Uri { get; init;  }
    }
}
