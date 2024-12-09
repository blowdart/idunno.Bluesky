// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.AtProto.Repo;

using idunno.Bluesky.Actor;

namespace idunno.Bluesky.Embed
{
    /// <summary>
    /// A view projection over a record embedded in a Bluesky record.
    /// </summary>

    // See https://github.com/bluesky-social/atproto/blob/828f17f53a34fd1194cb37beaa0cc8db42023717/lexicons/app/bsky/embed/record.json for definition.

    public sealed record ViewRecord : View
    {
        /// <summary>
        /// Constructs a new instance of <see cref="ViewRecord"/>
        /// </summary>
        /// <param name="uri">The <see cref="AtUri"/> of the record.</param>
        /// <param name="cid">The <see cref="AtProto.Cid"/> of the record.</param>
        /// <param name="author">A <see cref="ProfileViewBasic"/> of the record author.</param>
        /// <param name="labels">Any labels applied to the record.</param>
        /// <param name="replyCount">The number of replies to this post.</param>
        /// <param name="repostCount">The number of times this post has been reposted.</param>
        /// <param name="likeCount">The number of likes this record has.</param>
        /// <param name="quoteCount">The number of quotes this post has.</param>
        /// <param name="embeds">The views over any embeds the record has.</param>
        /// <param name="indexedAt">The <see cref="DateTimeOffset"/> the record was indexed on.</param>
        [JsonConstructor]
        public ViewRecord(
            AtUri uri,
            Cid cid,
            ProfileViewBasic author,
            IReadOnlyCollection<Label>? labels,
            int? replyCount,
            int? repostCount,
            int? likeCount,
            int? quoteCount,
            IReadOnlyCollection<EmbeddedView>? embeds,
            DateTimeOffset indexedAt)
        {
            Uri = uri;
            Cid = cid;
            StrongReference = new StrongReference(uri, cid);

            Author = author;
            Labels = labels ?? new List<Label>().AsReadOnly();
            ReplyCount = replyCount;
            RepostCount = repostCount;
            LikeCount = likeCount;
            QuoteCount = quoteCount;
            Embeds = embeds ?? new List<EmbeddedView>().AsReadOnly();
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
        public StrongReference StrongReference { get; }

        /// <summary>
        /// Gets the record author.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public ProfileViewBasic Author { get; init; }

        /// <summary>
        /// Gets the record data itself.
        /// </summary>
        [JsonIgnore]
        public JsonElement Value
        {
            get
            {
                return ExtensionData["value"];
            }
        }

        /// <summary>
        /// Gets any labels applied to the record.
        /// </summary>
        [JsonInclude]
        public IReadOnlyCollection<Label>? Labels { get; init; }

        /// <summary>
        /// Gets the number of replies to this post.
        /// </summary>
        [JsonInclude]
        public int? ReplyCount { get; init; }

        /// <summary>
        /// Gets the number of times this post has been reposted.
        /// </summary>
        [JsonInclude]
        public int? RepostCount { get; init; }

        /// <summary>
        /// Gets the number of likes this record has.
        /// </summary>
        [JsonInclude]
        public int? LikeCount { get; init; }

        /// <summary>
        /// Gets the number of quotes this post has.
        /// </summary>
        [JsonInclude]
        public int? QuoteCount { get; init; }

        /// <summary>
        /// Gets views over any embeds the record has.
        /// </summary>
        [NotNull]
        [JsonInclude]
        public IReadOnlyCollection<EmbeddedView>? Embeds { get; init; }

        /// <summary>
        /// Gets the <see cref="DateTimeOffset"/> the record was indexed on.
        /// </summary>
        [JsonInclude]
        public DateTimeOffset IndexedAt { get; init; }
    }
}
