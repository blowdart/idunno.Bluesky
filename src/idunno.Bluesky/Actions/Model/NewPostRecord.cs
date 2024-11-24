// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using idunno.AtProto;
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

            if (!string.IsNullOrEmpty(text) && (text.Length > Maximum.PostLengthInCharacters || text.GetLengthInGraphemes() > Maximum.PostLengthInGraphemes))
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

                    if (tag.Length > Maximum.TagLengthInCharacters || tag.GetLengthInGraphemes() > Maximum.TagLengthInGraphemes)
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

        [JsonInclude]
        [JsonPropertyName("$type")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Cannot be static as it won't be serialized.")]
        public string Type => RecordType.Post;

        [JsonInclude]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Text { get; internal set; }

        [JsonInclude]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public IList<Facet>? Facets { get; internal set; }

        [JsonInclude]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ReplyReferences? Reply { get; internal set; }

        [JsonInclude]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public EmbeddedBase? Embed { get; internal set; }

        [JsonInclude]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public IList<string>? Langs { get; internal set; } 

        [JsonInclude]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public IReadOnlyCollection<SelfLabels>? Labels { get; internal set; }

        [JsonInclude]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public IReadOnlyCollection<string>? Tags { get; internal set; }

        [JsonInclude]
        public DateTimeOffset CreatedAt { get; internal set; }
    }
}
