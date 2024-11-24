// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using idunno.AtProto.Labels;
using idunno.Bluesky.Embed;
using idunno.Bluesky.RichText;

namespace idunno.Bluesky.Feed
{
    /// <summary>
    /// An AT Proto record for a Bluesky post.
    /// </summary>
    /// <remarks><para>See <see href="https://github.com/bluesky-social/atproto/blob/main/lexicons/app/bsky/feed/post.json">post.json</see> for the lexicon definition.</para></remarks>
    public record PostRecord : BlueskyRecord
    {
        /// <summary>
        /// Creates a new instance of <see cref="PostRecord"/>.
        /// </summary>
        /// <param name="text">The text of the post, if any.</param>
        /// <param name="createdAt">The date and time the post record was created at.</param>
        /// <param name="facets">A list of facets to applied to the post.</param>
        /// <param name="langs">The languages the post is written in.</param>
        /// <param name="embed">Embedded references, if any.</param>
        /// <param name="reply">A optional <see cref="Reply"/>, if the post is part of a thread.</param>
        /// <param name="labels">Self labels for the post, if any</param>
        /// <param name="tags">Additional hashtags, in addition to any included in post text and facets</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="text"/> is null and <paramref name="embed"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Throw if <paramref name="text" /> is longer than the maximum size.</exception>
        /// <remarks>
        ///<para><paramref name="text"/> may be an empty string, if there are <paramref name="embed"/> is not null.</para>
        /// </remarks>
        public PostRecord(
            string? text,
            DateTimeOffset createdAt,
            IReadOnlyList<Facet> facets,
            IReadOnlyList<string>? langs,
            EmbeddedBase? embed,
            ReplyReferences? reply,
            SelfLabels? labels,
            IReadOnlyList<string>? tags) : base(createdAt)
        {
            Facets = facets;
            Langs = langs;
            Tags = tags;
            Text = text;
            Reply = reply;
            Embed = embed;
            Labels = labels;

            if (!string.IsNullOrWhiteSpace(text))
            {
                ArgumentOutOfRangeException.ThrowIfGreaterThan(text.Length, Maximum.PostLengthInCharacters);
                ArgumentOutOfRangeException.ThrowIfGreaterThan(text.GetLengthInGraphemes(), Maximum.PostLengthInGraphemes);
            }

            if (string.IsNullOrWhiteSpace(text) && embed is null)
            {
                throw new ArgumentNullException(nameof(text));
            }
        }

        /// <summary>
        /// The text of the post.
        /// </summary>
        [JsonInclude]
        public string? Text { get; init; }

        /// <summary>
        /// A list of facets to applied to the post.
        /// </summary>
        [JsonInclude]
        public IReadOnlyList<Facet>? Facets { get; init; }

        /// <summary>
        /// A <see cref="ReplyReferences"/>, if the post is part of a thread.
        /// </summary>
        [JsonInclude]
        public ReplyReferences? Reply { get; init; }

        [JsonInclude]
        /// <summary>
        /// An optional <see cref="EmbeddedBase" /> for embedded media information.
        /// </summary>
        public EmbeddedBase? Embed { get; init; }

        /// <summary>
        /// The languages the post is written in.
        /// </summary>
        [JsonInclude]
        public IReadOnlyList<string>? Langs { get; init; }

        /// <summary>
        /// Self-label values for this post. Effectively content warnings.
        /// </summary>
        [JsonInclude]
        public SelfLabels? Labels { get; init; }

        /// <summary>
        /// Additional hashtags, in addition to any included in post text and facets.
        /// </summary>
        [JsonInclude]
        public IReadOnlyList<string>? Tags { get; init; }
    }
}
