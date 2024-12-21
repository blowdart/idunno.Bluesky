// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto.Labels;
using idunno.Bluesky.Embed;
using idunno.Bluesky.RichText;

namespace idunno.Bluesky.Actions.Model
{
    /// <summary>
    /// Encapsulates the information needed to make a new post record.
    /// </summary>
    public sealed class NewPostRecord
    {
        internal NewPostRecord()
        {
        }

        internal NewPostRecord(NewPostRecord postRecord) : this(
            postRecord.Text,
            postRecord.Reply,
            postRecord.Facets,
            postRecord.Langs,
            postRecord.Embed,
            postRecord.Labels,
            postRecord.Tags,
            postRecord.CreatedAt)
        {
            ArgumentNullException.ThrowIfNull(postRecord);
        }

        /// <summary>
        /// Creates a new instance of <see cref="NewPostRecord"/>.
        /// </summary>
        /// <param name="text">The text for the post.</param>
        /// <param name="reply">The <see cref="ReplyReferences"/>, if any, of the post this post is in reply to.</param>
        /// <param name="facets">A collection of <see cref="Facet"/>s for the post.</param>
        /// <param name="langs">A collection of language strings, if any, that the post is written in.</param>
        /// <param name="embed">The embedded record for the post, if any.</param>
        /// <param name="labels">A collection of <see cref="SelfLabels"/> to apply to the post, if any.</param>
        /// <param name="tags">A collection of tags to apply to the post, if any.</param>
        /// <param name="createdAt">The <see cref="DateTimeOffset"/> the post was created on.</param>
        /// <exception cref="ArgumentException">
        ///    Thrown when <paramref name="text"/> is null or empty and there is no <paramref name="embed"/> record or
        ///    <paramref name="tags"/> contains an empty tag.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        ///    Thrown when <paramref name="text"/> exceeds the maximum length or.
        ///    <paramref name="tags"/> exceeds the maximum number of tags or has a value that exceeds the maximum tag length.
        ///    
        /// </exception>
        [JsonConstructor]
        public NewPostRecord(
            string? text,
            ReplyReferences? reply = null,
            IList<Facet>? facets = null,
            IList<string>? langs = null,
            EmbeddedBase? embed = null,
            IReadOnlyCollection<SelfLabels>? labels = null,
            IReadOnlyCollection<string>? tags = null,
            DateTimeOffset? createdAt = null)
        {
            if (embed is null && string.IsNullOrEmpty(text))
            {
                throw new ArgumentException($"{nameof(text)} cannot be null if {nameof(embed)} is null.", nameof(text));
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
            Labels = labels;

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

            if (createdAt is null)
            {
                CreatedAt = DateTimeOffset.UtcNow;
            }
            else
            {
                CreatedAt = (DateTimeOffset)createdAt;
            }
        }

        /// <summary>
        /// Gets the JSON record type for this instance.
        /// </summary>
        [JsonInclude]
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
        [JsonInclude]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public IReadOnlyCollection<SelfLabels>? Labels { get; internal set; }

        /// <summary>
        /// Gets the collection of tags to apply to the post, if any.
        /// </summary>
        [JsonInclude]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public IReadOnlyCollection<string>? Tags { get; internal set; }

        /// <summary>
        /// Gets the <see cref="DateTimeOffset"/> the post was created on.
        /// </summary>
        [JsonInclude]
        public DateTimeOffset CreatedAt { get; internal set; }

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
    }
}
