// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using idunno.AtProto.Labels;
using idunno.Bluesky.Embed;
using idunno.Bluesky.Record;
using idunno.Bluesky.RichText;

namespace idunno.Bluesky
{
    /// <summary>
    /// Encapsulates a Bluesky post.
    /// </summary>
    /// <remarks>
    /// <para>See <see href="https://github.com/bluesky-social/atproto/blob/main/lexicons/app/bsky/feed/post.json">post.json</see> for the lexicon definition.</para>
    /// </remarks>
    public sealed record class Post : BlueskyRecordValue
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
        /// Creates a new instance of <see cref="Post"/> and sets <see cref="BlueskyRecordValue.CreatedAt"/> to the current date and time.
        /// </summary>
        public Post() : base()
        {
        }

        /// <summary>
        /// Creates a new instance of <see cref="Post"/> from the specified <paramref name="postRecord"/>.
        /// </summary>
        /// <param name="postRecord">The <see cref="Post"/> to create the new instance from.</param>
        public Post(Post postRecord) : base(postRecord)
        {
            if (postRecord is not null)
            {
                Text = postRecord.Text;
                CreatedAt = postRecord.CreatedAt;
                Embed = postRecord.Embed;
                Reply = postRecord.Reply;

                if (postRecord.Facets is not null)
                {
                    Facets = new List<Facet>(postRecord.Facets);
                }

                if (postRecord.Langs is not null)
                {
                    Langs = new List<string>(postRecord.Langs);
                }

                if (postRecord.Labels is not null)
                {
                    Labels = new SelfLabels(postRecord.Labels.Values);
                }

                if (postRecord.Tags is not null)
                {
                    Tags = new List<string>(postRecord.Tags);
                }
            }
        }

        /// <summary>
        /// Creates a new instance of <see cref="Post"/>.
        /// </summary>
        /// <param name="text">The text for the post.</param>
        /// <param name="reply">The <see cref="ReplyReferences"/>, if any, of the post this post is in reply to.</param>
        /// <param name="facets">A collection of <see cref="Facet"/>s for the post.</param>
        /// <param name="langs">A collection of language strings, if any, that the post is written in.</param>
        /// <param name="embed">The embedded record for the post, if any.</param>
        /// <param name="labels">A collection of <see cref="SelfLabels"/> to apply to the post, if any.</param>
        /// <param name="tags">A collection of tags to apply to the post, if any.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="text"/> is null and <paramref name="embed"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        ///    Thrown when <paramref name="text"/> exceeds the maximum length or.
        ///    <paramref name="tags"/> exceeds the maximum number of tags or has a value that exceeds the maximum tag length.
        /// </exception>
        /// <remarks>
        ///<para><paramref name="text"/> may be an empty string, if there are <paramref name="embed"/> is not null.</para>
        /// </remarks>
        public Post(
            string? text,
            IList<Facet>? facets = null,
            IList<string>? langs = null,
            EmbeddedBase? embed = null,
            ReplyReferences? reply = null,
            SelfLabels? labels = null,
            IReadOnlyCollection<string>? tags = null) : this(text, DateTimeOffset.Now, facets, langs, embed, reply, labels, tags)
        {
        }

        /// <summary>
        /// Creates a new instance of <see cref="Post"/>.
        /// </summary>
        /// <param name="text">The text for the post.</param>
        /// <param name="reply">The <see cref="ReplyReferences"/>, if any, of the post this post is in reply to.</param>
        /// <param name="facets">A collection of <see cref="Facet"/>s for the post.</param>
        /// <param name="langs">A collection of language strings, if any, that the post is written in.</param>
        /// <param name="embed">The embedded record for the post, if any.</param>
        /// <param name="labels">A collection of <see cref="SelfLabels"/> to apply to the post, if any.</param>
        /// <param name="tags">A collection of tags to apply to the post, if any.</param>
        /// <param name="createdAt">The <see cref="DateTimeOffset"/> the post was created on.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="text"/> is null and <paramref name="embed"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        ///    Thrown when <paramref name="text"/> exceeds the maximum length or.
        ///    <paramref name="tags"/> exceeds the maximum number of tags or has a value that exceeds the maximum tag length.
        /// </exception>
        /// <remarks>
        ///<para><paramref name="text"/> may be an empty string, if there are <paramref name="embed"/> is not null.</para>
        /// </remarks>
        [JsonConstructor]
        public Post(
            string? text,
            DateTimeOffset? createdAt,
            IList<Facet>? facets = null,
            IList<string>? langs = null,
            EmbeddedBase? embed = null,
            ReplyReferences? reply = null,
            SelfLabels? labels = null,
            IReadOnlyCollection<string>? tags = null) : base(createdAt)
        {
            if (string.IsNullOrWhiteSpace(text) && embed is null)
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
            Embed = embed;

            if (labels is not null)
            {
                Labels = labels;
            }

            if (tags is not null)
            {
                if (tags.Count > Maximum.ExternalTagsInPost)
                {
                    throw new ArgumentOutOfRangeException(nameof(tags), $"Cannot contain more than {Maximum.ExternalTagsInPost} tags.");
                }

                int position = 0;
                foreach (string tag in tags)
                {
                    if (string.IsNullOrEmpty(tag))
                    {
                        throw new ArgumentException($"Tag[{position}] is null or empty", nameof(tags));
                    }

                    if (tag.Length > Maximum.TagLengthInCharacters || tag.GetGraphemeLength() > Maximum.TagLengthInGraphemes)
                    {
                        throw new ArgumentException($"Tag[{position}] is longer than {Maximum.TagLengthInCharacters} characters or {Maximum.TagLengthInGraphemes} graphemes", nameof(tags));
                    }
                    position++;
                }
            }
        }

        /// <summary>
        /// Gets the JSON record type for this instance.
        /// </summary>
        [JsonIgnore]
        [JsonPropertyName("$type")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Cannot be static as it won't be serialized.")]
        public string Type => RecordType.Post;

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
        public IList<Facet>? Facets { get; internal set; }

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
        public EmbeddedBase? Embed { get; internal set; }

        /// <summary>
        /// Gets the collection of language strings, if any, that the post is written in.
        /// </summary>
        [JsonInclude]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public IList<string>? Langs { get; internal set; }

        /// <summary>
        /// A collection of <see cref="SelfLabels"/> to apply to the post, if any.
        /// </summary>
        /// <remarks>
        /// <para>Post self labels can only be one of the known <see href="https://docs.bsky.app/docs/advanced-guides/moderation#global-label-values">global values</see>.</para>
        /// </remarks>
        [JsonInclude]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [NotNull]
        public SelfLabels? Labels { get; internal set; } = new SelfLabels();

        /// <summary>
        /// Gets the collection of tags to apply to the post, if any.
        /// </summary>
        [JsonInclude]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public IReadOnlyCollection<string>? Tags { get; internal set; }

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
                return Labels.Contains(PornLabelName);
            }

            set
            {
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
                return Labels.Contains(SexualLabelName);
            }

            set
            {
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
                return Labels.Contains(GraphicMediaLabelName);
            }

            set
            {
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
                return Labels.Contains(NudityLabelName);
            }

            set
            {
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
