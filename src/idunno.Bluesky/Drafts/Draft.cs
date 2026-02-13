// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.Bluesky.Feed.Gates;

namespace idunno.Bluesky.Drafts
{
    /// <summary>
    /// Encapsulates a draft containing an array of draft posts
    /// </summary>
    [JsonPolymorphic(UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToNearestAncestor)]
    [JsonDerivedType(typeof(Draft), typeDiscriminator: "app.bsky.draft.defs#draft")]
    public record Draft
    {
        /// <summary>
        /// Creates a new instance of <see cref="Draft"/> with the specified properties.
        /// </summary>
        /// <param name="posts">A list of posts for the draft. Maximum 100.</param>
        /// <param name="deviceId">UUIDv4 identifier of the device that created this draft.</param>
        /// <param name="deviceName">The device and/or platform on which the draft was created. Maximum 100 characters.</param>
        /// <param name="langs">A collection of RFC5646 language codes for the draft. Maximum 3.</param>
        /// <param name="postGateEmbeddingRules">An collection of embedding rules for the draft posts. Maximum 5.</param>
        /// <param name="threadGateAllowRules">A collection of thread gate rules for the draft. Maximum 5.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="posts"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when any of the collections, or <see cref="DeviceName"/> exceed their maximum allowed size.</exception>
        [JsonConstructor]
        public Draft(
            IList<DraftPost> posts,
            Guid? deviceId,
            string? deviceName = null,
            IList<string>? langs = null,
            IList<PostGateRule>? postGateEmbeddingRules = null,
            IList<ThreadGateRule>? threadGateAllowRules = null)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(
                deviceName?.Length ?? 0,
                100);

            ArgumentNullException.ThrowIfNull(posts);
            ArgumentOutOfRangeException.ThrowIfLessThan(
                posts.Count,
                1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(
                posts.Count,
                100);

            if (langs is not null)
            {
                ArgumentOutOfRangeException.ThrowIfLessThan(
                    langs.Count,
                    1);
                ArgumentOutOfRangeException.ThrowIfGreaterThan(
                    langs.Count,
                    3);
            }

            if (postGateEmbeddingRules is not null)
            {
                ArgumentOutOfRangeException.ThrowIfLessThan(
                    postGateEmbeddingRules.Count,
                    1);
                ArgumentOutOfRangeException.ThrowIfGreaterThan(
                    postGateEmbeddingRules.Count,
                    5);
            }

            if (threadGateAllowRules is not null)
            {
                ArgumentOutOfRangeException.ThrowIfLessThan(
                    threadGateAllowRules.Count,
                    1);
                ArgumentOutOfRangeException.ThrowIfGreaterThan(
                    threadGateAllowRules.Count,
                    5);
            }

            Posts = posts;
            DeviceId = deviceId;
            DeviceName = deviceName;
            Langs = langs;
            PostGateEmbeddingRules = postGateEmbeddingRules;
            ThreadGateAllowRules = threadGateAllowRules;
        }

        /// <summary>
        /// Creates a new instance of <see cref="Draft"/> with the specified properties.
        /// </summary>
        /// <param name="post">The draft post.</param>
        /// <param name="deviceId">UUIDv4 identifier of the device that created this draft.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="post"/> is <see langword="null"/>.</exception>
        public Draft(
            DraftPost post,
            Guid? deviceId): this(
                posts: [ post ],
                deviceId: deviceId,
                deviceName: null,
                langs: null,
                postGateEmbeddingRules: null,
                threadGateAllowRules: null)
        {
            ArgumentNullException.ThrowIfNull(post);
        }

        /// <summary>
        /// Creates a new instance of <see cref="Draft"/> with the specified properties.
        /// </summary>
        /// <param name="post">The draft post.</param>
        /// <param name="deviceId">UUIDv4 identifier of the device that created this draft.</param>
        /// <param name="deviceName">The device and/or platform on which the draft was created. Maximum 100 characters.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="post"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="deviceName"/> is specified and its length is greater than 100 characters.</exception>
        public Draft(
            DraftPost post,
            Guid? deviceId,
            string? deviceName) : this(
                posts: [post],
                deviceId: deviceId,
                deviceName: deviceName,
                langs: null,
                postGateEmbeddingRules: null,
                threadGateAllowRules: null)
        {
            ArgumentNullException.ThrowIfNull(post);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(
                deviceName?.Length ?? 0,
                100);
        }

        /// <summary>
        /// Creates a new instance of <see cref="Draft"/> with the specified properties.
        /// </summary>
        /// <param name="text">The text to create a draft post from.</param>
        /// <param name="deviceId">UUIDv4 identifier of the device that created this draft.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="text"/> is <see langword="null"/> or whitespace.</exception>
        public Draft(
            string text,
            Guid? deviceId) : this(
                posts: [new DraftPost(text)],
                deviceId: deviceId,
                deviceName: null,
                langs: null,
                postGateEmbeddingRules: null,
                threadGateAllowRules: null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(text);
        }

        /// <summary>
        /// Creates a new instance of <see cref="Draft"/> with the specified properties.
        /// </summary>
        /// <param name="text">The text to create a draft post from.</param>
        /// <param name="deviceId">UUIDv4 identifier of the device that created this draft.</param>
        /// <param name="deviceName">The device and/or platform on which the draft was created. Maximum 100 characters.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="text"/> is <see langword="null"/> or whitespace.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="deviceName"/> is specified and its length is greater than 100 characters.</exception>"
        public Draft(
            string text,
            Guid? deviceId,
            string? deviceName) : this(
                posts: [new DraftPost(text)],
                deviceId: deviceId,
                deviceName: deviceName,
                langs: null,
                postGateEmbeddingRules: null,
                threadGateAllowRules: null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(text);

            ArgumentOutOfRangeException.ThrowIfGreaterThan(
                deviceName?.Length ?? 0,
                100);
        }

        /// <summary>
        /// Gets the UUIDv4 identifier of the device that created this draft.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Guid? DeviceId { get; init; }

        /// <summary>
        /// Gets the device and/or platform on which the draft was created. Maximum 100 characters.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? DeviceName { get; init; }

        /// <summary>
        /// Gets the array of draft posts that compose this draft.
        /// </summary>
        [JsonRequired]
        public IList<DraftPost> Posts { get; init; }

        /// <summary>
        /// Gets the collection of language strings, if any, that the post is written in.
        /// </summary>
        /// <remarks>
        ///<para>A maximum of three languages can be specified. Languages should be specified in RFC5646 format.</para>
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number of languages is less than one or greater than three.</exception>"
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public IList<string>? Langs { get; init; }

        /// <summary>
        /// Gets the rules for the post gate to be created when this draft is published.
        /// </summary>
        /// <remarks><para>A maximum of five post gates can be specified.</para></remarks>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number of post gate rules is less than one or greater than five.</exception>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("postgateEmbeddingRules")]
        public IList<PostGateRule>? PostGateEmbeddingRules { get; init; } = null;

        /// <summary>
        /// Gets the rules for the thread gates to be created when this draft is published.
        /// </summary>
        /// <remarks><para>A maximum of five thread gates can be specified.</para></remarks>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number of thread gate rules is less than one or greater than five.</exception>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("threadgateAllow")]
        public IList<ThreadGateRule>? ThreadGateAllowRules { get; init; } = null;
    }
}
