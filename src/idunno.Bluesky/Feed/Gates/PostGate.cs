// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.AtProto.Repo;

namespace idunno.Bluesky.Feed.Gates
{
    // https://github.com/bluesky-social/atproto/blob/828f17f53a34fd1194cb37beaa0cc8db42023717/lexicons/app/bsky/feed/postgate.json

    /// <summary>
    /// Record defining interaction rules for a post.
    /// The record key (rkey) of the post gate record must match the record key of the post, and that record must be in the same repository.
    /// </summary>
    public sealed record PostGate : AtProtoRecord
    {
        /// <summary>
        /// Creates a new instance of <see cref="PostGate"/>.
        /// </summary>
        /// <param name="post">The <see cref="AtUri"/> of the post to apply the gate rules too.</param>
        /// <param name="rules">The list of rules for post gate, if any.</param>
        /// <param name="detachedEmbeddingUris">The list of <see cref="AtUri"/> posts embedding <paramref name="post"/> to be detached, if any.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="post"/> is null.</exception>
        public PostGate(AtUri post, ICollection<PostGateRule>? rules, ICollection<AtUri>? detachedEmbeddingUris = null)
        {
            ArgumentNullException.ThrowIfNull(post);

            if (post.Collection != CollectionNsid.Post)
            {
                throw new ArgumentException("does not point to a Post record", nameof(post));
            }

            Post = post;

            if (rules is not null && rules.Count != 0)
            {
                ArgumentOutOfRangeException.ThrowIfGreaterThan(rules.Count, Maximum.PostGateRules);
            }
            Rules = rules;

            if (detachedEmbeddingUris is not null && detachedEmbeddingUris.Count != 0)
            {
                ArgumentOutOfRangeException.ThrowIfGreaterThan(detachedEmbeddingUris.Count, Maximum.PostGateDetachedEmbeddingPosts);
            }
            DetachedEmbeddingUris = detachedEmbeddingUris;

        }

        /// <summary>
        /// Creates a new instance of <see cref="PostGate"/>.
        /// </summary>
        /// <param name="post">The <see cref="AtUri"/> of the post to apply the gate rules too.</param>
        /// <param name="createdAt">The <see cref="DateTimeOffset"/> the record was created on.</param>
        /// <param name="detachedEmbeddingUris">The list of <see cref="AtUri"/> posts embedding <paramref name="post"/> to be detached, if any.</param>
        /// <param name="rules">The list of rules for post gate, if any.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="createdAt"/> is null.</exception>
        public PostGate(AtUri post, DateTimeOffset createdAt, ICollection<PostGateRule>? rules, ICollection<AtUri>? detachedEmbeddingUris) :
            this(post, rules, detachedEmbeddingUris)
        {
            ArgumentNullException.ThrowIfNull(createdAt);

            CreatedAt = createdAt;
        }

        /// <summary>
        /// The JavaScript object type for the record.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Cannot be static, as json serializer will not include it.")]
        [JsonInclude]
        public string Type => RecordType.PostGate;

        /// <summary>
        /// Gets the <see cref="AtUri"/> reference to the post to be gated.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public AtUri Post { get; init; }

        /// <summary>
        /// Gets the <see cref="DateTimeOffset"/> when this record was created.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Gets the list of rules for this post gate, if any.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("embeddingRules")]
        public ICollection<PostGateRule>? Rules { get; init; }

        /// <summary>
        /// Gets the list of <see cref="AtUri"/>s of posts embedding the <see cref="Post"/> that the author has detached, if any.
        /// </summary>
        [JsonInclude]
        public ICollection<AtUri>? DetachedEmbeddingUris { get; init; }
    }
}
