// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text.Json.Serialization;

using idunno.AtProto.Labels;
using idunno.Bluesky.Embed;
using idunno.Bluesky.Record;
using idunno.Bluesky.RichText;

namespace idunno.Bluesky
{
    /// <summary>
    /// Encapsulates a Bluesky post record value.
    /// </summary>
    /// <remarks>
    /// <para>See <see href="https://github.com/bluesky-social/atproto/blob/main/lexicons/app/bsky/feed/post.json">post.json</see> for the lexicon definition.</para>
    /// </remarks>
    public sealed record class Post : BlueskyTimestampedRecord
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private const string PornLabelName = "porn";

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private const string SexualLabelName = "sexual";

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private const string GraphicMediaLabelName = "graphic-media";

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private const string NudityLabelName = "nudity";

        /// <summary>
        /// Creates a new instance of <see cref="Post"/> and sets <see cref="BlueskyTimestampedRecord.CreatedAt"/> to the current date and time.
        /// </summary>
        public Post() : base()
        {
        }

        /// <summary>
        /// Creates a new instance of <see cref="Post"/>.
        /// </summary>
        /// <param name="text">The text for the post.</param>
        /// <param name="reply">The <see cref="ReplyReferences"/>, if any, of the post this post is in reply to.</param>
        /// <param name="facets">A collection of <see cref="Facet"/>s for the post.</param>
        /// <param name="langs">A collection of language strings, if any, that the post is written in.</param>
        /// <param name="embeddedRecord">The embedded record for the post, if any.</param>
        /// <param name="labels">A collection of <see cref="SelfLabels"/> to apply to the post, if any.</param>
        /// <param name="tags">A collection of tags to apply to the post, if any.</param>
        /// <param name="createdAt">The <see cref="DateTimeOffset"/> the post was created on.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null and <paramref name="embeddedRecord"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        ///    Thrown when <paramref name="text"/> exceeds the maximum length or.
        ///    <paramref name="tags"/> exceeds the maximum number of tags or has a value that exceeds the maximum tag length.
        /// </exception>
        /// <remarks>
        ///<para><paramref name="text"/> may be an empty string, if there are <paramref name="embeddedRecord"/> is not null.</para>
        /// </remarks>
        [JsonConstructor]
        public Post(
            string? text,
            DateTimeOffset createdAt,
            ICollection<Facet>? facets = null,
            ICollection<string>? langs = null,
            EmbeddedBase? embeddedRecord = null,
            ReplyReferences? reply = null,
            SelfLabels? labels = null,
            ICollection<string>? tags = null) : base(createdAt)
        {
            if (string.IsNullOrWhiteSpace(text) && embeddedRecord is null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            if (!string.IsNullOrEmpty(text) && (text.Length > Maximum.PostLengthInCharacters || text.GetGraphemeLength() > Maximum.PostLengthInGraphemes))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(text),
                    $"text cannot have be longer than {Maximum.PostLengthInCharacters} characters, or {Maximum.PostLengthInGraphemes} graphemes.");
            }

            Text = text;
            Reply = reply;
            Facets = facets;
            Langs = langs;

            EmbeddedRecord = embeddedRecord;

            if (labels is not null)
            {
                Labels = labels;
            }

            if (tags is not null)
            {
                List<string> tagList = [.. tags];

                if (tagList.Count > Maximum.ExternalTagsInPost)
                {
                    throw new ArgumentOutOfRangeException(nameof(tags), $"Cannot contain more than {Maximum.ExternalTagsInPost} tags.");
                }

                int position = 0;
                foreach (string tag in tagList)
                {
                    if (string.IsNullOrEmpty(tag))
                    {
                        throw new ArgumentException($"Tag[{position}] is null or empty", nameof(tags));
                    }

                    if (tag.Length > Maximum.TagLengthInCharacters || tag.GetGraphemeLength() > Maximum.TagLengthInGraphemes)
                    {
                        throw new ArgumentOutOfRangeException(nameof(tags), $"Tag[{position}] is longer than {Maximum.TagLengthInCharacters} characters or {Maximum.TagLengthInGraphemes} graphemes");
                    }
                    position++;
                }

                Tags = tagList;
            }
        }

        /// <summary>
        /// Creates a new instance of <see cref="Post"/> from the specified <paramref name="post"/>.
        /// </summary>
        /// <param name="post">The <see cref="Post"/> to create the new instance from.</param>
        public Post(Post post) : base(post)
        {
            if (post is not null)
            {
                Text = post.Text;
                CreatedAt = post.CreatedAt;
                EmbeddedRecord = post.EmbeddedRecord;
                Reply = post.Reply;

                if (post.Facets is not null)
                {
                    Facets = [.. post.Facets];
                }

                if (post.Langs is not null)
                {
                    Langs = [.. post.Langs];
                }

                if (post.Labels is not null)
                {
                    Labels = new SelfLabels(post.Labels.Values);
                }

                if (post.Tags is not null)
                {
                    Tags = [.. post.Tags];
                }
            }
        }

        /// <summary>
        /// Creates a new instance of <see cref="Post"/>.
        /// </summary>
        /// <param name="text">The text for the post.</param>
        /// <param name="lang">The language the post is written in.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text" /> is null or empty.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="lang" /> is empty.</exception>
        public Post(
            string text,
            string lang) : this(text: text, langs: [lang])
        {
            ArgumentException.ThrowIfNullOrEmpty(text);
            ArgumentException.ThrowIfNullOrEmpty(lang);
        }

        /// <summary>
        /// Creates a new instance of <see cref="Post"/>.
        /// </summary>
        /// <param name="text">The text for the post.</param>
        /// <param name="langs">The languages the post is written in.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text" /> is null or empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="langs"/> is an empty list.</exception>
        public Post(
            string text,
            ICollection<string> langs) : this(text: text, facets: null, langs: langs, embeddedRecord: null, reply: null, labels : null, tags : null)
        {
            ArgumentException.ThrowIfNullOrEmpty(text);
            ArgumentNullException.ThrowIfNull(langs);

            ArgumentOutOfRangeException.ThrowIfZero(langs.Count);
        }

        /// <summary>
        /// Creates a new instance of <see cref="Post"/>.
        /// </summary>
        /// <param name="text">The text for the post.</param>
        /// <param name="reply">The <see cref="ReplyReferences"/>, if any, of the post this post is in reply to.</param>
        /// <param name="facets">A collection of <see cref="Facet"/>s for the post.</param>
        /// <param name="langs">A collection of language strings, if any, that the post is written in.</param>
        /// <param name="embeddedRecord">The embedded record for the post, if any.</param>
        /// <param name="labels">A collection of <see cref="SelfLabels"/> to apply to the post, if any.</param>
        /// <param name="tags">A collection of tags to apply to the post, if any.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null and <paramref name="embeddedRecord"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        ///    Thrown when <paramref name="text"/> exceeds the maximum length or.
        ///    <paramref name="tags"/> exceeds the maximum number of tags or has a value that exceeds the maximum tag length.
        /// </exception>
        /// <remarks>
        ///<para><paramref name="text"/> may be an empty string, if there are <paramref name="embeddedRecord"/> is not null.</para>
        /// </remarks>
        public Post(
            string? text,
            ICollection<Facet>? facets = null,
            ICollection<string>? langs = null,
            EmbeddedBase? embeddedRecord = null,
            ReplyReferences? reply = null,
            SelfLabels? labels = null,
            ICollection<string>? tags = null) : this(text, DateTimeOffset.Now, facets, langs, embeddedRecord, reply, labels, tags)
        {
        }

        /// <summary>
        /// Creates a new instance of <see cref="Post"/>.
        /// </summary>
        /// <param name="text">The text for the post.</param>
        /// <param name="reply">The <see cref="ReplyReferences"/>, if any, of the post this post is in reply to.</param>
        /// <param name="facets">A collection of <see cref="Facet"/>s for the post.</param>
        /// <param name="langs">A collection of language strings, if any, that the post is written in.</param>
        /// <param name="image">The <see cref="EmbeddedImage"/> to embed in the post..</param>
        /// <param name="labels">A collection of <see cref="SelfLabels"/> to apply to the post, if any.</param>
        /// <param name="tags">A collection of tags to apply to the post, if any.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is null.</exception>
        public Post(
            string? text,
            EmbeddedImage image,
            ICollection<Facet>? facets = null,
            ICollection<string>? langs = null,
            ReplyReferences? reply = null,
            SelfLabels? labels = null,
            ICollection<string>? tags = null)
            : this(text,
                createdAt: DateTimeOffset.UtcNow,
                facets: facets,
                langs: langs,
                new EmbeddedImages([image]),
                reply: reply,
                labels: labels,
                tags: tags)
        {
            ArgumentNullException.ThrowIfNull(image);
        }

        /// <summary>
        /// Creates a new instance of <see cref="Post"/>.
        /// </summary>
        /// <param name="text">The text for the post.</param>
        /// <param name="createdAt">The <see cref="DateTimeOffset"/> the post was created on.</param>
        /// <param name="reply">The <see cref="ReplyReferences"/>, if any, of the post this post is in reply to.</param>
        /// <param name="facets">A collection of <see cref="Facet"/>s for the post.</param>
        /// <param name="langs">A collection of language strings, if any, that the post is written in.</param>
        /// <param name="image">The <see cref="EmbeddedImage"/> to embed in the post..</param>
        /// <param name="labels">A collection of <see cref="SelfLabels"/> to apply to the post, if any.</param>
        /// <param name="tags">A collection of tags to apply to the post, if any.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is null.</exception>
        public Post(
            string? text,
            DateTimeOffset createdAt,
            EmbeddedImage image,
            ICollection<Facet>? facets = null,
            ICollection<string>? langs = null,
            ReplyReferences? reply = null,
            SelfLabels? labels = null,
            ICollection<string>? tags = null)
            : this (text,
                createdAt: createdAt,
                facets: facets,
                langs: langs,
                new EmbeddedImages([image]),
                reply : reply,
                labels: labels,
                tags: tags)
        {
            ArgumentNullException.ThrowIfNull(image);
        }

        /// <summary>
        /// Creates a new instance of <see cref="Post"/>.
        /// </summary>
        /// <param name="text">The text for the post.</param>
        /// <param name="reply">The <see cref="ReplyReferences"/>, if any, of the post this post is in reply to.</param>
        /// <param name="facets">A collection of <see cref="Facet"/>s for the post.</param>
        /// <param name="langs">A collection of language strings, if any, that the post is written in.</param>
        /// <param name="images">The <see cref="EmbeddedImage"/> to embed in the post..</param>
        /// <param name="labels">A collection of <see cref="SelfLabels"/> to apply to the post, if any.</param>
        /// <param name="tags">A collection of tags to apply to the post, if any.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="images"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="images"/> or empty, or contains more than the maximum allowed number of images.</exception>
        public Post(
            string? text,
            ICollection<EmbeddedImage> images,
            ICollection<Facet>? facets = null,
            ICollection<string>? langs = null,
            ReplyReferences? reply = null,
            SelfLabels? labels = null,
            ICollection<string>? tags = null)
            : this(text,
                createdAt: DateTimeOffset.UtcNow,
                facets: facets,
                langs: langs,
                new EmbeddedImages(images),
                reply: reply,
                labels: labels,
                tags: tags)
        {
            ArgumentNullException.ThrowIfNull(images);

            ArgumentOutOfRangeException.ThrowIfZero(images.Count);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(images.Count, Maximum.ImagesInPost);
        }

        /// <summary>
        /// Creates a new instance of <see cref="Post"/>.
        /// </summary>
        /// <param name="text">The text for the post.</param>
        /// <param name="createdAt">The <see cref="DateTimeOffset"/> the post was created on.</param>
        /// <param name="reply">The <see cref="ReplyReferences"/>, if any, of the post this post is in reply to.</param>
        /// <param name="facets">A collection of <see cref="Facet"/>s for the post.</param>
        /// <param name="langs">A collection of language strings, if any, that the post is written in.</param>
        /// <param name="images">The <see cref="EmbeddedImage"/> to embed in the post..</param>
        /// <param name="labels">A collection of <see cref="SelfLabels"/> to apply to the post, if any.</param>
        /// <param name="tags">A collection of tags to apply to the post, if any.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="images"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="images"/> or empty, or contains more than the maximum allowed number of images.</exception>
        public Post(
            string? text,
            DateTimeOffset createdAt,
            ICollection<EmbeddedImage> images,
            ICollection<Facet>? facets = null,
            ICollection<string>? langs = null,
            ReplyReferences? reply = null,
            SelfLabels? labels = null,
            ICollection<string>? tags = null)
            : this(text,
                createdAt: createdAt,
                facets: facets,
                langs: langs,
                new EmbeddedImages(images),
                reply: reply,
                labels: labels,
                tags: tags)
        {
            ArgumentNullException.ThrowIfNull(images);

            ArgumentOutOfRangeException.ThrowIfZero(images.Count);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(images.Count, Maximum.ImagesInPost);
        }

        /// <summary>
        /// Creates a new instance of <see cref="Post"/>.
        /// </summary>
        /// <param name="text">The text for the post.</param>
        /// <param name="video">The <see cref="EmbeddedVideo"/> to embed in the post.</param>
        /// <param name="reply">The <see cref="ReplyReferences"/>, if any, of the post this post is in reply to.</param>
        /// <param name="facets">A collection of <see cref="Facet"/>s for the post.</param>
        /// <param name="langs">A collection of language strings, if any, that the post is written in.</param>
        /// <param name="labels">A collection of <see cref="SelfLabels"/> to apply to the post, if any.</param>
        /// <param name="tags">A collection of tags to apply to the post, if any.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="video"/> is null.</exception>
        public Post(
            string? text,
            EmbeddedVideo video,
            ICollection<Facet>? facets = null,
            ICollection<string>? langs = null,
            ReplyReferences? reply = null,
            SelfLabels? labels = null,
            ICollection<string>? tags = null)
            : this(text,
                createdAt: DateTimeOffset.UtcNow,
                facets: facets,
                langs: langs,
                video,
                reply: reply,
                labels: labels,
                tags: tags)
        {
            ArgumentNullException.ThrowIfNull(video);
        }

        /// <summary>
        /// Creates a new instance of <see cref="Post"/>.
        /// </summary>
        /// <param name="text">The text for the post.</param>
        /// <param name="createdAt">The <see cref="DateTimeOffset"/> the post was created on.</param>
        /// <param name="video">The <see cref="EmbeddedVideo"/> to embed in the post.</param>
        /// <param name="reply">The <see cref="ReplyReferences"/>, if any, of the post this post is in reply to.</param>
        /// <param name="facets">A collection of <see cref="Facet"/>s for the post.</param>
        /// <param name="langs">A collection of language strings, if any, that the post is written in.</param>
        /// <param name="labels">A collection of <see cref="SelfLabels"/> to apply to the post, if any.</param>
        /// <param name="tags">A collection of tags to apply to the post, if any.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="video"/> is null.</exception>
        public Post(
            string? text,
            DateTimeOffset createdAt,
            EmbeddedVideo video,
            ICollection<Facet>? facets = null,
            ICollection<string>? langs = null,
            ReplyReferences? reply = null,
            SelfLabels? labels = null,
            ICollection<string>? tags = null)
            : this(text,
                createdAt: createdAt,
                facets: facets,
                langs: langs,
                video,
                reply: reply,
                labels: labels,
                tags: tags)
        {
            ArgumentNullException.ThrowIfNull(video);
        }

        /// <summary>
        /// Gets the text for the post, if any.
        /// </summary>
        [JsonInclude]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Text { get; internal set; }

        /// <summary>
        /// Gets a list of <see cref="Facet"/>s for the post, if any.
        /// </summary>
        [JsonInclude]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ICollection<Facet>? Facets { get; internal set; }

        /// <summary>
        /// The <see cref="ReplyReferences"/>, if any, of the post this post is in reply to.
        /// </summary>
        [JsonInclude]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ReplyReferences? Reply { get; internal set; }

        /// <summary>
        /// Gets the embedded record for the post, if any.
        /// </summary>
        [JsonInclude]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("embed")]
        public EmbeddedBase? EmbeddedRecord { get; internal set; }

        /// <summary>
        /// Gets the collection of language strings, if any, that the post is written in.
        /// </summary>
        [JsonInclude]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ICollection<string>? Langs { get; internal set; }

        /// <summary>
        /// A collection of <see cref="SelfLabels"/> to apply to the post, if any.
        /// </summary>
        /// <remarks>
        /// <para>Post self labels can only be one of the known <see href="https://docs.bsky.app/docs/advanced-guides/moderation#global-label-values">global values</see>.</para>
        /// </remarks>
        [JsonInclude]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public SelfLabels? Labels { get; internal set; }

        /// <summary>
        /// Gets the collection of tags to apply to the post, if any.
        /// </summary>
        [JsonInclude]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ICollection<string>? Tags { get; internal set; }

        /// <summary>
        /// Gets the number of characters in the post.
        /// </summary>
        [JsonIgnore]
        public int Length
        {
            get
            {
                if (Text is null)
                {
                    return 0;
                }
                else
                {
                    return Text.Length;
                }
            }
        }

        /// <summary>
        /// Gets the number of bytes in the post, as a Utf8 byte array.
        /// </summary>
        [JsonIgnore]
        public int Utf8Length
        {
            get
            {
                if (Text is null)
                {
                    return 0;
                }
                else
                {
                    return Text.GetUtf8Length();
                }
            }
        }

        /// <summary>
        /// Gets the number of graphemes in the post.
        /// </summary>
        [JsonIgnore]
        public int GraphemeLength
        {
            get
            {
                if (Text is null)
                {
                    return 0;
                }
                else
                {
                    return Text.GetGraphemeLength();
                }
            }
        }

        /// <summary>
        /// Gets or sets a flag indicating the post media contains porn.
        /// This puts a warning on images and can only be clicked through if the user is 18+ and has enabled adult content.
        /// </summary>
        [JsonIgnore]
        public bool ContainsPorn
        {
            get
            {
                if (Labels is not null)
                {
                    return Labels.Contains(PornLabelName);
                }
                else
                {
                    return false;
                }
            }

            set
            {
                Labels ??= new SelfLabels();
                if (value)
                {
                    Labels.AddLabel(PornLabelName);
                }
                else
                {
                    Labels.RemoveLabel(PornLabelName);
                }
            }
        }

        /// <summary>
        /// Gets or sets a flag indicating the post media contains sexual content.
        /// This behaves like <see cref="ContainsPorn"/> but is meant to handle less intense sexual content.
        /// </summary>
        [JsonIgnore]
        public bool ContainsSexualContent
        {
            get
            {
                if (Labels is not null)
                {
                    return Labels.Contains(SexualLabelName);
                }
                else
                {
                    return false;
                }
            }

            set
            {
                Labels ??= new SelfLabels();
                if (value)
                {
                    Labels.AddLabel(SexualLabelName);
                }
                else
                {
                    Labels.RemoveLabel(SexualLabelName);
                }
            }
        }

        /// <summary>
        /// Gets or sets a flag indicating the post media contains graphic media.
        /// This behaves like <see cref="ContainsPorn"/> but is for violence or gore.
        /// </summary>
        [JsonIgnore]
        public bool ContainsGraphicMedia
        {
            get
            {
                if (Labels is null)
                {
                    return false;
                }

                return Labels.Contains(GraphicMediaLabelName);
            }

            set
            {
                Labels ??= new SelfLabels();
                if (value)
                {
                    Labels.AddLabel(GraphicMediaLabelName);
                }
                else
                {
                    Labels.RemoveLabel(GraphicMediaLabelName);
                }
            }
        }

        /// <summary>
        /// Gets or sets a flag indicating the post media contains nudity.
        /// This puts a warning on images but isn't 18+ and defaults to ignore.
        /// </summary>
        [JsonIgnore]
        public bool ContainsNudity
        {
            get
            {
                if (Labels is null)
                {
                    return false;
                }

                return Labels.Contains(NudityLabelName);
            }

            set
            {
                Labels ??= new SelfLabels();

                if (value)
                {
                    Labels.AddLabel(NudityLabelName);
                }
                else
                {
                    Labels.RemoveLabel(NudityLabelName);
                }
            }
        }

        /// <summary>
        /// Sets the self labels for the post to the values specified in <paramref name="labels"/>.
        /// </summary>
        /// <param name="labels">The self label values to set.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="labels"/> is null.</exception>
        public void SetSelfLabels(PostSelfLabels labels)
        {
            ArgumentNullException.ThrowIfNull(labels);

            ContainsPorn = labels.Porn;
            ContainsSexualContent = labels.SexualContent;
            ContainsGraphicMedia = labels.GraphicMedia;
            ContainsNudity = labels.Nudity;
        }

        /// <summary>
        /// Embeds the specified <paramref name="embed"/> in this instance.
        /// </summary>
        /// <param name="embed">The record to embed in the post.</param>
        public void Embed(EmbeddedBase embed)
        {
            EmbeddedRecord = embed;
        }

        /// <summary>
        /// Removes the embedded record from the post.
        /// </summary>
        public void RemoveEmbed()
        {
            EmbeddedRecord = null;
        }
    }

    /// <summary>
    /// Properties for labels to apply to a post.
    /// </summary>
    public sealed record PostSelfLabels
    {
        /// <summary>
        /// Gets or sets a flag indicating the post media contains porn.
        /// This puts a warning on images and can only be clicked through if the user is 18+ and has enabled adult content.
        /// </summary>
        public bool Porn { get; set; }

        /// <summary>
        /// Gets or sets a flag indicating the post media contains sexual content.
        /// This behaves like <see cref="Porn"/> but is meant to handle less intense sexual content.
        /// </summary>
        public bool SexualContent { get; set; }

        /// <summary>
        /// Gets or sets a flag indicating the post media contains graphic media.
        /// This behaves like <see cref="Porn"/> but is for violence or gore.
        /// </summary>
        public bool GraphicMedia { get; set; }

        /// <summary>
        /// Gets or sets a flag indicating the post media contains nudity.
        /// This puts a warning on images but isn't 18+ and defaults to ignore.
        /// </summary>
        public bool Nudity { get; set; }
    }
}
