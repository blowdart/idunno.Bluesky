// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;

namespace idunno.Bluesky.Feed.Gates
{
    /// <summary>
    /// Record defining interaction gating rules for a thread (aka, reply controls).
    /// The record key (rkey) of the threadgate record must match the record key of the thread's root post,
    /// and that record must be in the same repository
    /// </summary>
    public sealed record ThreadGate
    {
        /// <summary>
        /// Creates a new instance of <see cref="ThreadGate"/>.
        /// </summary>
        /// <param name="post">The <see cref="AtUri"/> of the thread to be gated.</param>
        /// <param name="rules">The list of rules for replies to the specified <paramref name="post"/>.</param>
        /// <param name="hiddenRepliesUris">A list of reply <see cref="AtUri"/>s that will be hidden for <see cref="Post"/></param>
        /// <exception cref="ArgumentNullException">if post is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        ///   if <paramref name="post"/> does not point to a post record,
        ///   or if <paramref name="rules"/> or <paramref name="hiddenRepliesUris"/> have more than the maximum number of entries.
        /// </exception>
        public ThreadGate(AtUri post, ICollection<ThreadGateRule>? rules = null, ICollection<AtUri>? hiddenRepliesUris = null)
        {
            ArgumentNullException.ThrowIfNull(post);

            if (post.Collection != CollectionNsid.Post)
            {
                throw new ArgumentException("does not point to a Post record", nameof(post));
            }

            Post = post;

            if (rules is not null && rules.Count != 0)
            {
                ArgumentOutOfRangeException.ThrowIfGreaterThan(rules.Count, Maximum.ThreadGateRules);
            }
            Rules = rules;

            if (hiddenRepliesUris is not null && hiddenRepliesUris.Count != 0)
            {
                ArgumentOutOfRangeException.ThrowIfGreaterThan(hiddenRepliesUris.Count, Maximum.ThreadGateHiddenReplies);
            }

            HiddenReplies = hiddenRepliesUris;
        }

        /// <summary>
        /// Creates a new instance of <see cref="ThreadGate"/>.
        /// </summary>
        /// <param name="post">The <see cref="AtUri"/> of the thread to be gated.</param>
        /// <param name="createdAt">The <see cref="DateTimeOffset"/> when this record was created</param>
        /// <param name="rules">The list of rules for replies to the specified <paramref name="post"/>.</param>
        /// <param name="hiddenReplies">A list of reply <see cref="AtUri"/>s that will be hidden for <see cref="Post"/></param>
        /// <exception cref="ArgumentNullException">if <paramref name="createdAt"/> is null.</exception>
        [JsonConstructor]
        public ThreadGate(AtUri post, DateTimeOffset createdAt, ICollection<ThreadGateRule>? rules = null, ICollection<AtUri>? hiddenReplies = null) : this(post, rules, hiddenReplies)
        {
            ArgumentNullException.ThrowIfNull(createdAt);

            CreatedAt = createdAt;
        }

        /// <summary>
        /// The JavaScript object type for the record.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Cannot be static, as json serializer will not include it.")]
        [JsonInclude]
        public string Type => RecordType.ThreadGate;

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
        /// Gets the list of rules for replies for <see cref="Post"/>.
        /// </summary>
        [JsonInclude]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("allow")]
        public ICollection<ThreadGateRule>? Rules { get; init; }

        /// <summary>
        /// Gets a list of reply <see cref="AtUri"/>s that will be hidden for <see cref="Post"/>.
        /// </summary>
        [JsonInclude]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ICollection<AtUri>? HiddenReplies { get; init; }

        /// <summary>
        /// Gets a configured instance of <see cref="ThreadGate"/> for the specified <paramref name="post"/> which doesn't allow any replies.
        /// </summary>
        /// <param name="post">The <see cref="AtUri"/> of the post to gate.</param>
        /// <returns>A configured instance of <see cref="ThreadGate"/> which doesn't allow any replies.</returns>
        /// <exception cref="ArgumentNullException">if <see cref="Post"/> is null.</exception>
        public static ThreadGate NoReplies(AtUri post)
        {
            ArgumentNullException.ThrowIfNull(post);

            return new ThreadGate(post);
        }
    }
}
