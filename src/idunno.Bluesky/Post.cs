// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.Bluesky.Embed;
using idunno.Bluesky.Record;
using idunno.Bluesky.RichText;

namespace idunno.Bluesky;

/// <summary>
/// Encapsulates a Bluesky post record value.
/// </summary>
/// <remarks>
/// <para>See <see href="https://github.com/bluesky-social/atproto/blob/main/lexicons/app/bsky/feed/post.json">post.json</see> for the lexicon definition.</para>
/// </remarks>
[JsonPolymorphic(UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToBaseType)]
[JsonDerivedType(typeof(Post), typeDiscriminator: RecordType.Post)]
public record class Post : BlueskyTimestampedRecord, IValidatableObject, IRecordValidatable
{
    /// <summary>
    /// Creates a new instance of <see cref="Post"/> and sets <see cref="BlueskyTimestampedRecord.CreatedAt"/> to the current date and time.
    /// </summary>
    public Post() : this(TimeProvider.System)
    {
    }

    internal Post(TimeProvider timeProvider) : base(timeProvider.GetUtcNow())
    {
        Text = string.Empty;
    }

    /// <summary>
    /// Creates a new instance of <see cref="Post"/>.
    /// </summary>
    /// <param name="text">The text for the post.</param>
    /// <param name="createdAt">The <see cref="DateTimeOffset"/> the post was created on.</param>
    /// <param name="facets">A collection of <see cref="Facet"/>s for the post.</param>
    /// <param name="langs">A collection of language strings, if any, that the post is written in.</param>
    /// <param name="embeddedRecord">The embedded record for the post, if any.</param>
    /// <param name="reply">The <see cref="ReplyReferences"/>, if any, of the post this post is in reply to.</param>
    /// <param name="labels">A collection of <see cref="SelfLabels"/> to apply to the post, if any.</param>
    /// <param name="tags">A collection of tags to apply to the post, if any.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> and <paramref name="embeddedRecord"/> are <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A tag position is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///    Thrown when <paramref name="text"/> exceeds the maximum length or.
    ///    <paramref name="tags"/> exceeds the maximum number of tags or has a value that exceeds the maximum tag length.
    /// </exception>
    /// <remarks>
    /// <para><paramref name="text"/> may be empty regardless of whether <paramref name="embeddedRecord"/> is present. A <see langword="null"/> value is allowed when <paramref name="embeddedRecord"/> is present and is normalised to an empty string. Nonempty whitespace-only text is permitted and preserved unchanged.</para>
    /// </remarks>
    public Post(
        string? text,
        DateTimeOffset createdAt,
        ICollection<Facet>? facets = null,
        ICollection<string>? langs = null,
        EmbeddedBase? embeddedRecord = null,
        ReplyReferences? reply = null,
        SelfLabels? labels = null,
        ICollection<string>? tags = null) : this(
            createdAt: createdAt,
            facets: facets,
            langs: langs,
            embeddedRecord: embeddedRecord,
            reply: reply,
            labels: labels,
            tags: tags)
    {
        if (embeddedRecord is null)
        {
            ArgumentNullException.ThrowIfNull(text);
        }

        if (!string.IsNullOrEmpty(text) && (text.GetUtf8Length() > Maximum.PostLengthInBytes || text.GetGraphemeLength() > Maximum.PostLengthInGraphemes))
        {
            throw new ArgumentOutOfRangeException(
                nameof(text),
                $"text cannot be longer than {Maximum.PostLengthInBytes} UTF-8 bytes, or {Maximum.PostLengthInGraphemes} graphemes.");
        }

        Text = text ?? string.Empty;
    }

    [JsonConstructor]
    internal Post(
        DateTimeOffset createdAt,
        string text,
        ICollection<Facet>? facets,
        ICollection<string>? langs,
        EmbeddedBase? embeddedRecord,
        ReplyReferences? reply,
        SelfLabels? labels,
        ICollection<string>? tags) : this(text, createdAt, facets, langs, embeddedRecord, reply, labels, tags)
    {
    }

    private Post(PostJsonValue record, EmbeddedBase? embeddedRecord, ICollection<Facet>? facets) : base(record.CreatedAt)
    {
        Text = record.Text;
        Facets = facets;
        Langs = record.Langs;
        EmbeddedRecord = embeddedRecord;
        Reply = record.Reply;
        Labels = record.Labels;
        Tags = record.Tags;
        ExtensionData = record.ExtensionData
            .Where(property => property.Key != "$type")
            .ToDictionary(property => property.Key, property => property.Value.Clone());
    }

    /// <summary>
    /// Creates a new instance of <see cref="Post"/> from the specified <paramref name="post"/>.
    /// </summary>
    /// <param name="post">The <see cref="Post"/> to create the new instance from.</param>
    public Post(Post post) : base(post)
    {
        UnnormalizedText = null;
        if (post is not null)
        {
            UnnormalizedText = post.UnnormalizedText;
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
    /// <exception cref="ArgumentNullException"><paramref name="text"/> or <paramref name="lang"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="lang" /> is empty.</exception>
    public Post(
        string text,
        string lang) : this(text: text, langs: [lang])
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrEmpty(lang);
    }

    /// <summary>
    /// Creates a new instance of <see cref="Post"/>.
    /// </summary>
    /// <param name="text">The text for the post.</param>
    /// <param name="langs">The languages the post is written in.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text" /> or <paramref name="langs"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="langs"/> is empty.</exception>
    public Post(
        string text,
        ICollection<string> langs) : this(text: text, facets: null, langs: langs, embeddedRecord: null, reply: null, labels: null, tags: null)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(langs);

        ArgumentOutOfRangeException.ThrowIfZero(langs.Count);
    }

    /// <summary>
    /// Creates a new instance of <see cref="Post"/>.
    /// </summary>
    /// <param name="text">The text for the post.</param>
    /// <param name="facets">A collection of <see cref="Facet"/>s for the post.</param>
    /// <param name="langs">A collection of language strings, if any, that the post is written in.</param>
    /// <param name="embeddedRecord">The embedded record for the post, if any.</param>
    /// <param name="reply">The <see cref="ReplyReferences"/>, if any, of the post this post is in reply to.</param>
    /// <param name="labels">A collection of <see cref="SelfLabels"/> to apply to the post, if any.</param>
    /// <param name="tags">A collection of tags to apply to the post, if any.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> and <paramref name="embeddedRecord"/> are <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A tag position is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///    Thrown when <paramref name="text"/> exceeds the maximum length or.
    ///    <paramref name="tags"/> exceeds the maximum number of tags or has a value that exceeds the maximum tag length.
    /// </exception>
    /// <remarks>
    /// <para><paramref name="text"/> may be empty regardless of whether <paramref name="embeddedRecord"/> is present, and may be <see langword="null"/> if <paramref name="embeddedRecord"/> is present. Nonempty whitespace-only text is permitted and preserved unchanged.</para>
    /// </remarks>
    public Post(
        string? text,
        ICollection<Facet>? facets = null,
        ICollection<string>? langs = null,
        EmbeddedBase? embeddedRecord = null,
        ReplyReferences? reply = null,
        SelfLabels? labels = null,
        ICollection<string>? tags = null) : this(text, TimeProvider.System, facets, langs, embeddedRecord, reply, labels, tags)
    {
    }

    internal Post(
        string? text,
        TimeProvider timeProvider,
        ICollection<Facet>? facets = null,
        ICollection<string>? langs = null,
        EmbeddedBase? embeddedRecord = null,
        ReplyReferences? reply = null,
        SelfLabels? labels = null,
        ICollection<string>? tags = null) : this(text, timeProvider.GetLocalNow(), facets, langs, embeddedRecord, reply, labels, tags)
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="Post"/>.
    /// </summary>
    /// <param name="text">The text for the post.</param>
    /// <param name="image">The <see cref="EmbeddedImage"/> to embed in the post..</param>
    /// <param name="facets">A collection of <see cref="Facet"/>s for the post.</param>
    /// <param name="langs">A collection of language strings, if any, that the post is written in.</param>
    /// <param name="reply">The <see cref="ReplyReferences"/>, if any, of the post this post is in reply to.</param>
    /// <param name="labels">A collection of <see cref="SelfLabels"/> to apply to the post, if any.</param>
    /// <param name="tags">A collection of tags to apply to the post, if any.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
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
            tags: tags) => ArgumentNullException.ThrowIfNull(image);

    /// <summary>
    /// Creates a new instance of <see cref="Post"/>.
    /// </summary>
    /// <param name="text">The text for the post.</param>
    /// <param name="createdAt">The <see cref="DateTimeOffset"/> the post was created on.</param>
    /// <param name="image">The <see cref="EmbeddedImage"/> to embed in the post..</param>
    /// <param name="facets">A collection of <see cref="Facet"/>s for the post.</param>
    /// <param name="langs">A collection of language strings, if any, that the post is written in.</param>
    /// <param name="reply">The <see cref="ReplyReferences"/>, if any, of the post this post is in reply to.</param>
    /// <param name="labels">A collection of <see cref="SelfLabels"/> to apply to the post, if any.</param>
    /// <param name="tags">A collection of tags to apply to the post, if any.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    public Post(
        string? text,
        DateTimeOffset createdAt,
        EmbeddedImage image,
        ICollection<Facet>? facets = null,
        ICollection<string>? langs = null,
        ReplyReferences? reply = null,
        SelfLabels? labels = null,
        ICollection<string>? tags = null)
        : this(text,
            createdAt: createdAt,
            facets: facets,
            langs: langs,
            new EmbeddedImages([image]),
            reply: reply,
            labels: labels,
            tags: tags) => ArgumentNullException.ThrowIfNull(image);

    /// <summary>
    /// Creates a new instance of <see cref="Post"/>.
    /// </summary>
    /// <param name="text">The text for the post.</param>
    /// <param name="images">The <see cref="EmbeddedImage"/> to embed in the post..</param>
    /// <param name="facets">A collection of <see cref="Facet"/>s for the post.</param>
    /// <param name="langs">A collection of language strings, if any, that the post is written in.</param>
    /// <param name="reply">The <see cref="ReplyReferences"/>, if any, of the post this post is in reply to.</param>
    /// <param name="labels">A collection of <see cref="SelfLabels"/> to apply to the post, if any.</param>
    /// <param name="tags">A collection of tags to apply to the post, if any.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="images"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="images"/> or empty, or contains more than the maximum allowed number of images.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="images"/> contains images without aspect ratios when more than the maximum allowed number of images are included.</exception>
    [SuppressMessage("Minor Code Smell", "S3267:Loops should be simplified with \"LINQ\" expressions", Justification = "Avoid linq in hot paths")]
    public Post(
        string? text,
        ICollection<EmbeddedImage> images,
        ICollection<Facet>? facets = null,
        ICollection<string>? langs = null,
        ReplyReferences? reply = null,
        SelfLabels? labels = null,
        ICollection<string>? tags = null)
        : this(
            createdAt: DateTimeOffset.UtcNow,
            facets: facets,
            langs: langs,
            embeddedRecord: null,
            reply: reply,
            labels: labels,
            tags: tags)
    {
        ArgumentNullException.ThrowIfNull(images);

        ArgumentOutOfRangeException.ThrowIfZero(images.Count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(images.Count, Maximum.GalleryItems);

        if (images is EmbeddedGallery imagesGallery)
        {
            EmbeddedRecord = imagesGallery;
        }
        else
        {
            if (images.Count > Maximum.ImagesInPost)
            {
                foreach (EmbeddedImage image in images)
                {
                    if (image.AspectRatio is null)
                    {
                        throw new ArgumentException($"Each image must have an aspect ratio when more than {Maximum.ImagesInPost} images are included.", nameof(images));
                    }
                }

                EmbeddedRecord = new EmbeddedGallery(images);
            }
            else
            {
                EmbeddedRecord = new EmbeddedImages(images);
            }
        }

        if (!string.IsNullOrEmpty(text) && (text.GetUtf8Length() > Maximum.PostLengthInBytes || text.GetGraphemeLength() > Maximum.PostLengthInGraphemes))
        {
            throw new ArgumentOutOfRangeException(
                nameof(text),
                $"text cannot be longer than {Maximum.PostLengthInBytes} UTF-8 bytes, or {Maximum.PostLengthInGraphemes} graphemes.");
        }
        Text = text ?? string.Empty;
    }

    /// <summary>
    /// Creates a new instance of <see cref="Post"/>.
    /// </summary>
    /// <param name="text">The text for the post.</param>
    /// <param name="createdAt">The <see cref="DateTimeOffset"/> the post was created on.</param>
    /// <param name="images">The <see cref="EmbeddedImage"/> to embed in the post..</param>
    /// <param name="facets">A collection of <see cref="Facet"/>s for the post.</param>
    /// <param name="langs">A collection of language strings, if any, that the post is written in.</param>
    /// <param name="reply">The <see cref="ReplyReferences"/>, if any, of the post this post is in reply to.</param>
    /// <param name="labels">A collection of <see cref="SelfLabels"/> to apply to the post, if any.</param>
    /// <param name="tags">A collection of tags to apply to the post, if any.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="images"/> is <see langword="null"/>.</exception>
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
    /// <param name="facets">A collection of <see cref="Facet"/>s for the post.</param>
    /// <param name="langs">A collection of language strings, if any, that the post is written in.</param>
    /// <param name="reply">The <see cref="ReplyReferences"/>, if any, of the post this post is in reply to.</param>
    /// <param name="labels">A collection of <see cref="SelfLabels"/> to apply to the post, if any.</param>
    /// <param name="tags">A collection of tags to apply to the post, if any.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="video"/> is <see langword="null"/>.</exception>
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
            tags: tags) => ArgumentNullException.ThrowIfNull(video);

    /// <summary>
    /// Creates a new instance of <see cref="Post"/>.
    /// </summary>
    /// <param name="text">The text for the post.</param>
    /// <param name="createdAt">The <see cref="DateTimeOffset"/> the post was created on.</param>
    /// <param name="video">The <see cref="EmbeddedVideo"/> to embed in the post.</param>
    /// <param name="facets">A collection of <see cref="Facet"/>s for the post.</param>
    /// <param name="langs">A collection of language strings, if any, that the post is written in.</param>
    /// <param name="reply">The <see cref="ReplyReferences"/>, if any, of the post this post is in reply to.</param>
    /// <param name="labels">A collection of <see cref="SelfLabels"/> to apply to the post, if any.</param>
    /// <param name="tags">A collection of tags to apply to the post, if any.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="video"/> is <see langword="null"/>.</exception>
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
            tags: tags) => ArgumentNullException.ThrowIfNull(video);

    /// <summary>
    /// Creates a new instance of <see cref="Post"/>.
    /// </summary>
    /// <param name="text">The text for the post.</param>
    /// <param name="createdAt">The <see cref="DateTimeOffset"/> the post was created on.</param>
    /// <param name="gallery">The <see cref="EmbeddedGallery"/> to embed in the post.</param>
    /// <param name="facets">A collection of <see cref="Facet"/>s for the post.</param>
    /// <param name="langs">A collection of language strings, if any, that the post is written in.</param>
    /// <param name="reply">The <see cref="ReplyReferences"/>, if any, of the post this post is in reply to.</param>
    /// <param name="labels">A collection of <see cref="SelfLabels"/> to apply to the post, if any.</param>
    /// <param name="tags">A collection of tags to apply to the post, if any.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="gallery"/> is <see langword="null"/>.</exception>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Convenience constructors")]
    public Post(
        string? text,
        DateTimeOffset createdAt,
        EmbeddedGallery gallery,
        ICollection<Facet>? facets = null,
        ICollection<string>? langs = null,
        ReplyReferences? reply = null,
        SelfLabels? labels = null,
        ICollection<string>? tags = null)
        : this(text,
            createdAt: createdAt,
            facets: facets,
            langs: langs,
            embeddedRecord: gallery,
            reply: reply,
            labels: labels,
            tags: tags) => ArgumentNullException.ThrowIfNull(gallery);

    /// <summary>
    /// Creates a new instance of <see cref="Post"/>.
    /// </summary>
    /// <param name="text">The text for the post.</param>
    /// <param name="gallery">The <see cref="EmbeddedGallery"/> to embed in the post.</param>
    /// <param name="facets">A collection of <see cref="Facet"/>s for the post.</param>
    /// <param name="langs">A collection of language strings, if any, that the post is written in.</param>
    /// <param name="reply">The <see cref="ReplyReferences"/>, if any, of the post this post is in reply to.</param>
    /// <param name="labels">A collection of <see cref="SelfLabels"/> to apply to the post, if any.</param>
    /// <param name="tags">A collection of tags to apply to the post, if any.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="gallery"/> is <see langword="null"/>.</exception>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Convenience constructors")]
    public Post(
        string? text,
        EmbeddedGallery gallery,
        ICollection<Facet>? facets = null,
        ICollection<string>? langs = null,
        ReplyReferences? reply = null,
        SelfLabels? labels = null,
        ICollection<string>? tags = null)
        : this(text,
            createdAt: DateTimeOffset.UtcNow,
            facets: facets,
            langs: langs,
            embeddedRecord: gallery,
            reply: reply,
            labels: labels,
            tags: tags) => ArgumentNullException.ThrowIfNull(gallery);

    private Post(
        DateTimeOffset createdAt,
        ICollection<Facet>? facets,
        ICollection<string>? langs,
        EmbeddedBase? embeddedRecord,
        ReplyReferences? reply,
        SelfLabels? labels,
        ICollection<string>? tags) : base(createdAt)
    {
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
                if (tag is null)
                {
                    throw new ArgumentException($"Tag[{position}] is null", nameof(tags));
                }

                if (tag.GetUtf8Length() > Maximum.TagLengthInBytes || tag.GetGraphemeLength() > Maximum.TagLengthInGraphemes)
                {
                    throw new ArgumentOutOfRangeException(nameof(tags), $"Tag[{position}] is longer than {Maximum.TagLengthInBytes} UTF-8 bytes or {Maximum.TagLengthInGraphemes} graphemes");
                }
                position++;
            }

            Tags = tagList;
        }
    }

    /// <summary>
    /// Parses a JSON document into a <see cref="Post"/>.
    /// </summary>
    /// <param name="document">The JSON document to parse.</param>
    /// <returns>A <see cref="Post"/> deserialized from the JSON document.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="JsonException">The JSON document does not contain a valid <see cref="Post"/>.</exception>
    public static Post Parse(JsonDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        return Parse(document.RootElement, validate: true);
    }

    /// <summary>
    /// Parses a JSON document into a <see cref="Post"/>.
    /// </summary>
    /// <param name="document">The JSON document to parse.</param>
    /// <param name="validate"><see langword="true"/> to reject posts that fail validation; otherwise, <see langword="false"/>.</param>
    /// <returns>A <see cref="Post"/> deserialized from the JSON document.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="JsonException">The JSON document does not contain a valid <see cref="Post"/>.</exception>
    public static Post Parse(JsonDocument document, bool validate)
    {
        ArgumentNullException.ThrowIfNull(document);

        return Parse(document.RootElement, validate);
    }

    /// <summary>
    /// Parses a JSON element into a <see cref="Post"/>.
    /// </summary>
    /// <param name="element">The JSON element to parse.</param>
    /// <returns>A <see cref="Post"/> deserialized from the JSON element.</returns>
    /// <exception cref="JsonException">The JSON element does not contain a valid <see cref="Post"/>.</exception>
    public static Post Parse(JsonElement element)
    {
        return Parse(element, validate: true);
    }

    /// <summary>
    /// Parses a JSON element into a <see cref="Post"/>.
    /// </summary>
    /// <param name="element">The JSON element to parse.</param>
    /// <param name="validate"><see langword="true"/> to reject posts that fail validation; otherwise, <see langword="false"/>.</param>
    /// <returns>A <see cref="Post"/> deserialized from the JSON element.</returns>
    /// <exception cref="JsonException">The JSON element does not contain a valid <see cref="Post"/>.</exception>
    public static Post Parse(JsonElement element, bool validate)
    {
        if (TryDeserialize(element, validate, out Post? result))
        {
            return result;
        }

        throw new JsonException("The JSON value does not contain a post.");
    }

    /// <summary>
    /// Tries to parse a JSON document into a <see cref="Post"/>.
    /// </summary>
    /// <param name="document">The JSON document to parse.</param>
    /// <param name="result">The parsed <see cref="Post"/> if successful; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the JSON document contains a valid <see cref="Post"/>; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(JsonDocument? document, [MaybeNullWhen(false)] out Post result)
    {
        return TryParse(document, validate: true, out result);
    }

    /// <summary>
    /// Tries to parse a JSON document into a <see cref="Post"/>.
    /// </summary>
    /// <param name="document">The JSON document to parse.</param>
    /// <param name="validate"><see langword="true"/> to reject posts that fail validation; otherwise, <see langword="false"/>.</param>
    /// <param name="result">The parsed <see cref="Post"/> if successful; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the JSON document contains a post that passes the selected validation mode; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(JsonDocument? document, bool validate, [MaybeNullWhen(false)] out Post result)
    {
        result = null;

        return document is not null && TryDeserialize(document.RootElement, validate, out result);
    }

    /// <summary>
    /// Tries to parse a JSON element into a <see cref="Post"/>.
    /// </summary>
    /// <param name="element">The JSON element to parse.</param>
    /// <param name="result">The parsed <see cref="Post"/> if successful; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the JSON element contains a valid <see cref="Post"/>; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(JsonElement element, [MaybeNullWhen(false)] out Post result)
    {
        return TryParse(element, validate: true, out result);
    }

    /// <summary>
    /// Tries to parse a JSON element into a <see cref="Post"/>.
    /// </summary>
    /// <param name="element">The JSON element to parse.</param>
    /// <param name="validate"><see langword="true"/> to reject posts that fail validation; otherwise, <see langword="false"/>.</param>
    /// <param name="result">The parsed <see cref="Post"/> if successful; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the JSON element contains a post that passes the selected validation mode; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(JsonElement element, bool validate, [MaybeNullWhen(false)] out Post result)
    {
        return TryDeserialize(element, validate, out result);
    }

    private static bool TryDeserialize(JsonElement element, bool validate, [MaybeNullWhen(false)] out Post result)
    {
        result = null;

        if (element.ValueKind is JsonValueKind.Undefined)
        {
            return false;
        }

        try
        {
            result = Json.RecordJson.Read(element, SourceGenerationContext.Default.Post);
            if (result is null)
            {
                return false;
            }

            if (validate && result.ValidatePost().Any())
            {
                result = null;
                return false;
            }

            return true;
        }
        catch (JsonException)
        {
            result = null;
            return !validate && TryDeserializeUnvalidated(element, out result);
        }
        catch (ArgumentException)
        {
            result = null;
            return !validate && TryDeserializeUnvalidated(element, out result);
        }
    }

    private static bool TryDeserializeUnvalidated(JsonElement element, [MaybeNullWhen(false)] out Post result)
    {
        result = null;

        try
        {
            result = DeserializeUnvalidated(element);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static Post DeserializeUnvalidated(JsonElement element)
    {
        PostJsonValue record = Json.RecordJson.Read(element, SourceGenerationContext.Default.PostJsonValue)
            ?? throw new JsonException("The JSON value does not contain a post.");

        EmbeddedBase? embeddedRecord = null;
        if (record.EmbeddedRecord is JsonElement embed && embed.ValueKind is not JsonValueKind.Null)
        {
            embeddedRecord = JsonSerializer.Deserialize(embed, Json.TolerantMediaJson.EmbeddedBaseTypeInfo);
        }

        ICollection<Facet>? facets = record.Facets?.Select(DeserializeUnvalidatedFacet).ToList();

        return new Post(record, embeddedRecord, facets);
    }

    internal static Facet DeserializeUnvalidatedFacet(JsonElement element)
    {
        FacetJsonValue facet = Json.RecordJson.Read(element, SourceGenerationContext.Default.FacetJsonValue)
            ?? throw new JsonException("A facet must be an object.");

        if (facet.Index is null || facet.Features is null)
        {
            throw new JsonException("A facet must contain an index and features.");
        }

        List<FacetFeature> features = [];
        foreach (JsonElement feature in facet.Features)
        {
            features.Add(DeserializeUnvalidatedFacetFeature(feature));
        }

        return new Facet(ByteSlice.CreateUnvalidated(facet.Index.ByteStart, facet.Index.ByteEnd), features)
        {
            ExtensionData = element.EnumerateObject()
                .Where(property => !property.Name.Equals("index", StringComparison.OrdinalIgnoreCase) &&
                    !property.Name.Equals("features", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(property => property.Name, property => property.Value.Clone())
        };
    }

    private static FacetFeature DeserializeUnvalidatedFacetFeature(JsonElement element)
    {
        if (element.ValueKind is JsonValueKind.Object &&
            element.TryGetProperty("$type", out JsonElement discriminator) &&
            discriminator.ValueKind is JsonValueKind.String &&
            discriminator.GetString() is "app.bsky.richtext.facet#tag")
        {
            TagFacetFeatureJsonValue tagFeature = Json.RecordJson.Read(element, SourceGenerationContext.Default.TagFacetFeatureJsonValue)
                ?? throw new JsonException("A tag facet feature must be an object.");

            if (tagFeature.Tag is null)
            {
                throw new JsonException("A tag facet feature must contain a tag.");
            }

            return TagFacetFeature.CreateUnvalidated(tagFeature.Tag);
        }

        return Json.RecordJson.Read(element, SourceGenerationContext.Default.FacetFeature)
            ?? throw new JsonException("A facet feature cannot be null.");
    }

    /// <summary>
    /// Validates the post text and tags against Bluesky's post constraints.
    /// </summary>
    /// <param name="validationContext">The context in which the validation is performed.</param>
    /// <returns>A collection of validation errors, or an empty collection when the post is valid.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="validationContext"/> is <see langword="null"/>.</exception>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        ArgumentNullException.ThrowIfNull(validationContext);

        return ValidatePost();
    }

    IEnumerable<ValidationResult> IRecordValidatable.ValidateRecord()
    {
        return ValidatePost();
    }

    private IEnumerable<ValidationResult> ValidatePost()
    {
        string? text = UnnormalizedText;
        if (text is null && EmbeddedRecord is null)
        {
            yield return new ValidationResult(
                "Text cannot be null unless the post contains an embedded record.",
                [nameof(Text)]);
        }

        if (!string.IsNullOrEmpty(text) &&
            (text.GetUtf8Length() > Maximum.PostLengthInBytes || text.GetGraphemeLength() > Maximum.PostLengthInGraphemes))
        {
            yield return new ValidationResult(
                $"Text must not exceed {Maximum.PostLengthInBytes} UTF-8 bytes or {Maximum.PostLengthInGraphemes} graphemes.",
                [nameof(Text)]);
        }

        if (Tags is not null)
        {
            if (Tags.Count > Maximum.ExternalTagsInPost)
            {
                yield return new ValidationResult(
                    $"Tags must not contain more than {Maximum.ExternalTagsInPost} tags.",
                    [nameof(Tags)]);
            }

            int position = 0;
            foreach (string? tag in Tags)
            {
                if (tag is null)
                {
                    yield return new ValidationResult($"Tag at position {position} cannot be null.", [nameof(Tags)]);
                }
                else if (tag.GetUtf8Length() > Maximum.TagLengthInBytes || tag.GetGraphemeLength() > Maximum.TagLengthInGraphemes)
                {
                    yield return new ValidationResult(
                        $"Tag at position {position} must not exceed {Maximum.TagLengthInBytes} UTF-8 bytes or {Maximum.TagLengthInGraphemes} graphemes.",
                        [nameof(Tags)]);
                }

                position++;
            }
        }

        foreach (ValidationResult result in RecordValidation.Collection(Facets, nameof(Facets), int.MaxValue)
            .Concat(RecordValidation.Collection(Langs, nameof(Langs), 3)))
        {
            yield return result;
        }

        foreach (ValidationResult result in ValidateEmbeddedRecord())
        {
            yield return result;
        }
    }

    internal sealed class PostJsonValue
    {
        [JsonRequired]
        public DateTimeOffset CreatedAt { get; init; }

        [JsonRequired]
        public string? Text { get; init; }

        public ICollection<JsonElement>? Facets { get; init; }

        public ICollection<string>? Langs { get; init; }

        [JsonPropertyName("embed")]
        public JsonElement? EmbeddedRecord { get; init; }

        public ReplyReferences? Reply { get; init; }

        public SelfLabels? Labels { get; init; }

        public ICollection<string>? Tags { get; init; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement> ExtensionData { get; set; } = [];
    }

    internal sealed class FacetJsonValue
    {
        [JsonRequired]
        public ByteSliceJsonValue? Index { get; init; }

        [JsonRequired]
        public ICollection<JsonElement>? Features { get; init; }

    }

    internal sealed class ByteSliceJsonValue
    {
        [JsonRequired]
        public long ByteStart { get; init; }

        [JsonRequired]
        public long ByteEnd { get; init; }
    }

    internal sealed class TagFacetFeatureJsonValue
    {
        [JsonRequired]
        public string? Tag { get; init; }
    }

    private IEnumerable<ValidationResult> ValidateEmbeddedRecord()
    {
        if (EmbeddedRecord is IRecordValidatable validatable)
        {
            foreach (ValidationResult result in PrefixValidationResults(validatable.ValidateRecord(), nameof(EmbeddedRecord)))
            {
                yield return result;
            }
        }
    }

    private static IEnumerable<ValidationResult> PrefixValidationResults(IEnumerable<ValidationResult> results, string prefix)
    {
        foreach (ValidationResult result in results)
        {
            string[] memberNames = [.. result.MemberNames.Select(memberName => $"{prefix}.{memberName}")];
            yield return new ValidationResult(result.ErrorMessage, memberNames);
        }
    }

    /// <summary>
    /// Gets the text for the post. Embedded-only posts use an empty string.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public string? Text
    {
        get => UnnormalizedText ?? string.Empty;
        internal set => UnnormalizedText = value;
    }

    internal string? UnnormalizedText { get; private set; }

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
                return Labels.Contains(SelfLabelValues.Porn);
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
                Labels.AddLabel(SelfLabelValues.Porn);
            }
            else
            {
                Labels.RemoveLabel(SelfLabelValues.Porn);
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
                return Labels.Contains(SelfLabelValues.Sexual);
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
                Labels.AddLabel(SelfLabelValues.Sexual);
            }
            else
            {
                Labels.RemoveLabel(SelfLabelValues.Sexual);
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

            return Labels.Contains(SelfLabelValues.GraphicMedia);
        }

        set
        {
            Labels ??= new SelfLabels();
            if (value)
            {
                Labels.AddLabel(SelfLabelValues.GraphicMedia);
            }
            else
            {
                Labels.RemoveLabel(SelfLabelValues.GraphicMedia);
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

            return Labels.Contains(SelfLabelValues.Nudity);
        }

        set
        {
            Labels ??= new SelfLabels();

            if (value)
            {
                Labels.AddLabel(SelfLabelValues.Nudity);
            }
            else
            {
                Labels.RemoveLabel(SelfLabelValues.Nudity);
            }
        }
    }

    /// <summary>
    /// Sets the self labels for the post to the values specified in <paramref name="labels"/>.
    /// </summary>
    /// <param name="labels">The self label values to set.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="labels"/> is <see langword="null"/>.</exception>
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

    /// <summary>
    /// Determines whether the specified <see cref="Post"/> is equal to the current instance.
    /// </summary>
    /// <param name="other">The <see cref="Post"/> to compare against the current instance.</param>
    /// <returns><see langword="true" /> if <paramref name="other"/> is equal to the current instance, otherwise <see langword="false" />.</returns>
    /// <remarks>
    /// <para>
    ///   <see cref="Facets"/>, <see cref="Langs"/> and <see cref="Tags"/> are compared by their contents rather than by reference.
    ///   <see cref="BlueskyTimestampedRecord.CreatedAt"/> takes part in the comparison, so two posts created at different times are never equal.
    /// </para>
    /// </remarks>
    public virtual bool Equals(Post? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (other is null || EqualityContract != other.EqualityContract)
        {
            return false;
        }

        return base.Equals(other) &&
            Text == other.Text &&
            Reply == other.Reply &&
            EmbeddedRecord == other.EmbeddedRecord &&
            Equals(Labels, other.Labels) &&
            CollectionComparison.SequenceEquals(Facets, other.Facets) &&
            CollectionComparison.SequenceEquals(Langs, other.Langs) &&
            CollectionComparison.SequenceEquals(Tags, other.Tags);
    }

    /// <summary>
    /// Returns the hash code for the current instance.
    /// </summary>
    /// <returns>The hash code for the current instance.</returns>
    /// <remarks>
    /// <para>
    ///   The hash code is derived from the current property values, so it changes if the instance is mutated.
    ///   Do not mutate an instance while it is being used as a key in a hashed collection.
    /// </para>
    /// </remarks>
    [SuppressMessage("Major Code Smell", "S2328:\"GetHashCode\" should not reference mutable fields", Justification = "Value equality over mutable properties requires the hash code to be derived from the same state, which is documented on the member.")]
    public override int GetHashCode()
    {
        HashCode hashCode = new();

        hashCode.Add(base.GetHashCode());
        hashCode.Add(Text, StringComparer.Ordinal);
        hashCode.Add(Reply);
        hashCode.Add(EmbeddedRecord);
        hashCode.Add(Labels);
        CollectionComparison.AddSequence(ref hashCode, Facets);
        CollectionComparison.AddSequence(ref hashCode, Langs);
        CollectionComparison.AddSequence(ref hashCode, Tags);

        return hashCode.ToHashCode();
    }
}
