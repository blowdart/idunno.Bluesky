// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Embed;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Creates a quote post containing a video.
    /// </summary>
    /// <param name="strongReference">A reference to the post to quote.</param>
    /// <param name="video">The video to attach to the quote.</param>
    /// <param name="tags">Any tags to apply to the quote post.</param>
    /// <param name="cancellationToken">A cancellation token for the operation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="strongReference"/> or <paramref name="video"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="tags"/> contains a <see langword="null"/> or empty tag.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The number of tags or a tag's UTF-8 byte or grapheme length exceeds the permitted maximum.</exception>
    /// <exception cref="AuthenticationRequiredException">The agent is not authenticated.</exception>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Convenience overloads preserve existing call patterns.")]
    public Task<AtProtoHttpResult<CreateRecordResult>> Quote(
        StrongReference strongReference,
        EmbeddedVideo video,
        ICollection<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        return Quote(rKey: null, strongReference, video, tags, cancellationToken);
    }

    /// <summary>
    /// Creates a quote post containing text and a video.
    /// </summary>
    /// <param name="strongReference">A reference to the post to quote.</param>
    /// <param name="text">The text for the quote post.</param>
    /// <param name="video">The video to attach to the quote.</param>
    /// <param name="tags">Any tags to apply to the quote post.</param>
    /// <param name="cancellationToken">A cancellation token for the operation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="strongReference"/>, <paramref name="text"/>, or <paramref name="video"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="tags"/> contains a <see langword="null"/> or empty tag.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The text's UTF-8 byte or grapheme length, number of tags, or a tag's UTF-8 byte or grapheme length exceeds the permitted maximum.</exception>
    /// <exception cref="AuthenticationRequiredException">The agent is not authenticated.</exception>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Convenience overloads preserve existing call patterns.")]
    public Task<AtProtoHttpResult<CreateRecordResult>> Quote(
        StrongReference strongReference,
        string text,
        EmbeddedVideo video,
        ICollection<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        return Quote(rKey: null, strongReference, text, video, tags, cancellationToken);
    }

    /// <summary>
    /// Creates a quote post containing a video using the specified record key.
    /// </summary>
    /// <param name="rKey">The record key to use, or <see langword="null"/> for a generated key.</param>
    /// <param name="strongReference">A reference to the post to quote.</param>
    /// <param name="video">The video to attach to the quote.</param>
    /// <param name="tags">Any tags to apply to the quote post.</param>
    /// <param name="cancellationToken">A cancellation token for the operation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="strongReference"/> or <paramref name="video"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="tags"/> contains a <see langword="null"/> or empty tag.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The number of tags or a tag's UTF-8 byte or grapheme length exceeds the permitted maximum.</exception>
    /// <exception cref="AuthenticationRequiredException">The agent is not authenticated.</exception>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "The explicit record key preserves existing call patterns.")]
    public Task<AtProtoHttpResult<CreateRecordResult>> Quote(
        RecordKey? rKey,
        StrongReference strongReference,
        EmbeddedVideo video,
        ICollection<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        return QuoteWithMedia(rKey, strongReference, string.Empty, video, tags, cancellationToken);
    }

    /// <summary>
    /// Creates a quote post containing text and a video using the specified record key.
    /// </summary>
    /// <param name="rKey">The record key to use, or <see langword="null"/> for a generated key.</param>
    /// <param name="strongReference">A reference to the post to quote.</param>
    /// <param name="text">The text for the quote post.</param>
    /// <param name="video">The video to attach to the quote.</param>
    /// <param name="tags">Any tags to apply to the quote post.</param>
    /// <param name="cancellationToken">A cancellation token for the operation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="strongReference"/>, <paramref name="text"/>, or <paramref name="video"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="tags"/> contains a <see langword="null"/> or empty tag.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The text's UTF-8 byte or grapheme length, number of tags, or a tag's UTF-8 byte or grapheme length exceeds the permitted maximum.</exception>
    /// <exception cref="AuthenticationRequiredException">The agent is not authenticated.</exception>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "The explicit record key preserves existing call patterns.")]
    public Task<AtProtoHttpResult<CreateRecordResult>> Quote(
        RecordKey? rKey,
        StrongReference strongReference,
        string text,
        EmbeddedVideo video,
        ICollection<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        return QuoteWithMedia(rKey, strongReference, text, video, tags, cancellationToken);
    }

    /// <summary>
    /// Creates a quote post containing an external card.
    /// </summary>
    /// <param name="strongReference">A reference to the post to quote.</param>
    /// <param name="externalCard">The external card to attach to the quote.</param>
    /// <param name="tags">Any tags to apply to the quote post.</param>
    /// <param name="cancellationToken">A cancellation token for the operation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="strongReference"/> or <paramref name="externalCard"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="tags"/> contains a <see langword="null"/> or empty tag.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The number of tags or a tag's UTF-8 byte or grapheme length exceeds the permitted maximum.</exception>
    /// <exception cref="AuthenticationRequiredException">The agent is not authenticated.</exception>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Convenience overloads preserve existing call patterns.")]
    public Task<AtProtoHttpResult<CreateRecordResult>> Quote(
        StrongReference strongReference,
        EmbeddedExternal externalCard,
        ICollection<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        return Quote(rKey: null, strongReference, externalCard, tags, cancellationToken);
    }

    /// <summary>
    /// Creates a quote post containing text and an external card.
    /// </summary>
    /// <param name="strongReference">A reference to the post to quote.</param>
    /// <param name="text">The text for the quote post.</param>
    /// <param name="externalCard">The external card to attach to the quote.</param>
    /// <param name="tags">Any tags to apply to the quote post.</param>
    /// <param name="cancellationToken">A cancellation token for the operation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="strongReference"/>, <paramref name="text"/>, or <paramref name="externalCard"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="tags"/> contains a <see langword="null"/> or empty tag.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The text's UTF-8 byte or grapheme length, number of tags, or a tag's UTF-8 byte or grapheme length exceeds the permitted maximum.</exception>
    /// <exception cref="AuthenticationRequiredException">The agent is not authenticated.</exception>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Convenience overloads preserve existing call patterns.")]
    public Task<AtProtoHttpResult<CreateRecordResult>> Quote(
        StrongReference strongReference,
        string text,
        EmbeddedExternal externalCard,
        ICollection<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        return Quote(rKey: null, strongReference, text, externalCard, tags, cancellationToken);
    }

    /// <summary>
    /// Creates a quote post containing an external card using the specified record key.
    /// </summary>
    /// <param name="rKey">The record key to use, or <see langword="null"/> for a generated key.</param>
    /// <param name="strongReference">A reference to the post to quote.</param>
    /// <param name="externalCard">The external card to attach to the quote.</param>
    /// <param name="tags">Any tags to apply to the quote post.</param>
    /// <param name="cancellationToken">A cancellation token for the operation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="strongReference"/> or <paramref name="externalCard"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="tags"/> contains a <see langword="null"/> or empty tag.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The number of tags or a tag's UTF-8 byte or grapheme length exceeds the permitted maximum.</exception>
    /// <exception cref="AuthenticationRequiredException">The agent is not authenticated.</exception>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "The explicit record key preserves existing call patterns.")]
    public Task<AtProtoHttpResult<CreateRecordResult>> Quote(
        RecordKey? rKey,
        StrongReference strongReference,
        EmbeddedExternal externalCard,
        ICollection<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        return QuoteWithMedia(rKey, strongReference, string.Empty, externalCard, tags, cancellationToken);
    }

    /// <summary>
    /// Creates a quote post containing text and an external card using the specified record key.
    /// </summary>
    /// <param name="rKey">The record key to use, or <see langword="null"/> for a generated key.</param>
    /// <param name="strongReference">A reference to the post to quote.</param>
    /// <param name="text">The text for the quote post.</param>
    /// <param name="externalCard">The external card to attach to the quote.</param>
    /// <param name="tags">Any tags to apply to the quote post.</param>
    /// <param name="cancellationToken">A cancellation token for the operation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="strongReference"/>, <paramref name="text"/>, or <paramref name="externalCard"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="tags"/> contains a <see langword="null"/> or empty tag.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The text's UTF-8 byte or grapheme length, number of tags, or a tag's UTF-8 byte or grapheme length exceeds the permitted maximum.</exception>
    /// <exception cref="AuthenticationRequiredException">The agent is not authenticated.</exception>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "The explicit record key preserves existing call patterns.")]
    public Task<AtProtoHttpResult<CreateRecordResult>> Quote(
        RecordKey? rKey,
        StrongReference strongReference,
        string text,
        EmbeddedExternal externalCard,
        ICollection<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        return QuoteWithMedia(rKey, strongReference, text, externalCard, tags, cancellationToken);
    }

    /// <summary>
    /// Creates a post record, with the supplied <paramref name="text"/>, quoting the post identified by <see cref="StrongReference"/>.
    /// </summary>
    /// <param name="strongReference">A <see cref="StrongReference"/> to the post to be quoted.</param>
    /// <param name="text">The text for the new post.</param>
    /// <param name="tags">Any tags to apply to the quote post.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="tags"/> contains a <see langword="null"/> or empty tag.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="strongReference"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   Thrown when the text length is longer than the maximum permitted or
    ///   <paramref name="tags"/> contains a tag whose length is greater than the maximum allowed characters or graphemes.
    /// </exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<CreateRecordResult>> Quote(
        StrongReference strongReference,
        string text,
        ICollection<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(strongReference);

        if (tags is not null)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(tags.Count, Maximum.TagsInPost);

            foreach (string tag in tags)
            {
                ArgumentException.ThrowIfNullOrEmpty(tag);
                ArgumentOutOfRangeException.ThrowIfGreaterThan(tag.GetUtf8Length(), Maximum.TagLengthInBytes);
                ArgumentOutOfRangeException.ThrowIfGreaterThan(tag.GetGraphemeLength(), Maximum.TagLengthInGraphemes);
            }
        }

        return await Quote(
            strongReference: strongReference,
            text: text,
            images: null,
            tags: tags,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a quote post using the specified <paramref name="rKey"/>.
    /// </summary>
    /// <param name="rKey">The record key to use for the quote post.</param>
    /// <param name="strongReference">A <see cref="StrongReference"/> to the post to be quoted.</param>
    /// <param name="text">The text for the new post.</param>
    /// <param name="tags">Any tags to apply to the quote post.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="tags"/> contains a <see langword="null"/> or empty tag.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="strongReference"/> or <paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the text or tags exceed their maximum lengths.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "The caller-supplied-key overload preserves the existing signature and cancellation-token call patterns.")]
    public Task<AtProtoHttpResult<CreateRecordResult>> Quote(
        RecordKey? rKey,
        StrongReference strongReference,
        string text,
        ICollection<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        return Quote(
            rKey,
            strongReference,
            text,
            images: null,
            tags: tags,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Creates a simple Bluesky post record with the specified <paramref name="text"/>, if any, and <paramref name="image" />, quoting the post identified by <see cref="StrongReference"/>.
    /// </summary>
    /// <param name="strongReference">A <see cref="StrongReference"/> to the post to be quoted.</param>
    /// <param name="text">The text for the post</param>
    /// <param name="image">The image to attach to the post.</param>
    /// <param name="tags">Any tags to apply to the quote post.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="text"/> is <see langword="null"/> or <paramref name="tags"/> contains a <see langword="null"/> or empty tag.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="strongReference"/> or <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   Thrown when <paramref name="text"/>'s length is greater than the maximum allowed characters or graphemes or
    ///   <paramref name="tags"/> contains a tag whose length is greater than the maximum allowed characters or graphemes.
    /// </exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<CreateRecordResult>> Quote(
        StrongReference strongReference,
        string text,
        EmbeddedImage image,
        ICollection<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(strongReference);
        ArgumentException.ThrowIfNullOrEmpty(text);
        ArgumentNullException.ThrowIfNull(image);

        if (tags is not null)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(tags.Count, Maximum.TagsInPost);

            foreach (string tag in tags)
            {
                ArgumentException.ThrowIfNullOrEmpty(tag);
                ArgumentOutOfRangeException.ThrowIfGreaterThan(tag.GetUtf8Length(), Maximum.TagLengthInBytes);
                ArgumentOutOfRangeException.ThrowIfGreaterThan(tag.GetGraphemeLength(), Maximum.TagLengthInGraphemes);
            }
        }

        return await Quote(
            strongReference: strongReference,
            text: text,
            images: [image],
            tags: tags,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a quote post with the specified <paramref name="rKey"/> and image.
    /// </summary>
    /// <param name="rKey">The record key to use for the quote post.</param>
    /// <param name="strongReference">A <see cref="StrongReference"/> to the post to be quoted.</param>
    /// <param name="text">The text for the new post.</param>
    /// <param name="image">The image to attach to the quote post.</param>
    /// <param name="tags">Any tags to apply to the quote post.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="text"/> or a tag is <see langword="null"/> or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="strongReference"/> or <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the text or tags exceed their maximum lengths.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "The caller-supplied-key overload preserves the existing signature and cancellation-token call patterns.")]
    public Task<AtProtoHttpResult<CreateRecordResult>> Quote(
        RecordKey? rKey,
        StrongReference strongReference,
        string text,
        EmbeddedImage image,
        ICollection<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(strongReference);
        ArgumentException.ThrowIfNullOrEmpty(text);
        ArgumentNullException.ThrowIfNull(image);

        return Quote(
            rKey,
            strongReference,
            text,
            images: [image],
            tags: tags,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Creates a simple Bluesky post record with the specified <paramref name="text"/>, if any, and <paramref name="images" />, quoting the post identified by <see cref="StrongReference"/>.
    /// </summary>
    /// <param name="strongReference">A <see cref="StrongReference"/> to the post to be quoted.</param>
    /// <param name="text">The text for the new post</param>
    /// <param name="images">Any images to attach to the post.</param>
    /// <param name="tags">Any tags to apply to the quote post.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="tags"/> contains a <see langword="null"/> or empty tag.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="strongReference"/> is <see langword="null"/> or <paramref name="text"/> is <see langword="null"/> or empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   Thrown when <paramref name="text"/>'s length is greater than the maximum allowed characters or graphemes or
    ///   <paramref name="tags"/> contains a tag whose length is greater than the maximum allowed characters or graphemes.
    /// </exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<CreateRecordResult>> Quote(
        StrongReference strongReference,
        string text,
        ICollection<EmbeddedImage>? images,
        ICollection<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(strongReference);
        ArgumentNullException.ThrowIfNull(text);

        if (text.GetUtf8Length() > Maximum.PostLengthInBytes || text.GetGraphemeLength() > Maximum.PostLengthInGraphemes)
        {
            throw new ArgumentOutOfRangeException(nameof(text), $"text cannot be longer than {Maximum.PostLengthInBytes} UTF-8 bytes, or {Maximum.PostLengthInGraphemes} graphemes.");
        }

        if (images is not null && images.Count > Maximum.ImagesInPost)
        {
            throw new ArgumentOutOfRangeException(nameof(images), $"cannot have more than {Maximum.ImagesInPost} images.");
        }

        if (images is not null && images.Count == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(images), $"cannot be an empty collection.");
        }

        if (tags is not null)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(tags.Count, Maximum.TagsInPost);

            foreach (string tag in tags)
            {
                ArgumentException.ThrowIfNullOrEmpty(tag);
                ArgumentOutOfRangeException.ThrowIfGreaterThan(tag.GetUtf8Length(), Maximum.TagLengthInBytes);
                ArgumentOutOfRangeException.ThrowIfGreaterThan(tag.GetGraphemeLength(), Maximum.TagLengthInGraphemes);
            }
        }

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        PostBuilder postBuilder = new(text, lang: Thread.CurrentThread.CurrentUICulture.Name, tags: tags)
        {
            QuotePost = strongReference,
        };

        if (images is not null)
        {
            postBuilder.Add(images);
        }

        return await Post(postBuilder, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a quote post with the specified <paramref name="rKey"/> and images.
    /// </summary>
    /// <param name="rKey">The record key to use for the quote post.</param>
    /// <param name="strongReference">A <see cref="StrongReference"/> to the post to be quoted.</param>
    /// <param name="text">The text for the new post.</param>
    /// <param name="images">Any images to attach to the quote post.</param>
    /// <param name="tags">Any tags to apply to the quote post.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="strongReference"/> or <paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the text, image count, or tags exceed their maximums.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="images"/> contains an image that cannot be added to the quote.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "The caller-supplied-key overload preserves the existing signature and cancellation-token call patterns.")]
    public async Task<AtProtoHttpResult<CreateRecordResult>> Quote(
        RecordKey? rKey,
        StrongReference strongReference,
        string text,
        ICollection<EmbeddedImage>? images,
        ICollection<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(strongReference);
        ArgumentNullException.ThrowIfNull(text);

        if (text.GetUtf8Length() > Maximum.PostLengthInBytes || text.GetGraphemeLength() > Maximum.PostLengthInGraphemes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(text),
                $"text cannot be longer than {Maximum.PostLengthInBytes} UTF-8 bytes, or {Maximum.PostLengthInGraphemes} graphemes.");
        }

        if (images is not null && images.Count > Maximum.ImagesInPost)
        {
            throw new ArgumentOutOfRangeException(nameof(images), $"cannot have more than {Maximum.ImagesInPost} images.");
        }

        if (images is not null && images.Count == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(images), "cannot be an empty collection.");
        }

        if (tags is not null)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(tags.Count, Maximum.TagsInPost);

            foreach (string tag in tags)
            {
                ArgumentException.ThrowIfNullOrEmpty(tag);
                ArgumentOutOfRangeException.ThrowIfGreaterThan(tag.GetUtf8Length(), Maximum.TagLengthInBytes);
                ArgumentOutOfRangeException.ThrowIfGreaterThan(tag.GetGraphemeLength(), Maximum.TagLengthInGraphemes);
            }
        }

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        PostBuilder postBuilder = new(text, lang: Thread.CurrentThread.CurrentUICulture.Name, tags: tags)
        {
            QuotePost = strongReference,
        };

        if (images is not null)
        {
            postBuilder.Add(images);
        }

        return await Post(rKey, postBuilder, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates an Bluesky post record quoting the post identified by <see cref="StrongReference"/> with just an image.
    /// </summary>
    /// <param name="strongReference">A <see cref="StrongReference"/> to the post to be quoted.</param>
    /// <param name="image">The image to attach to the quote.</param>
    /// <param name="tags">Any tags to apply to the quote post.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="tags"/> contains a <see langword="null"/> or empty tag.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/></exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   Thrown when <paramref name="tags"/> contains a tag whose length is greater than the maximum allowed characters or graphemes.
    /// </exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<CreateRecordResult>> Quote(
        StrongReference strongReference,
        EmbeddedImage image,
        ICollection<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (tags is not null)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(tags.Count, Maximum.TagsInPost);

            foreach (string tag in tags)
            {
                ArgumentException.ThrowIfNullOrEmpty(tag);
                ArgumentOutOfRangeException.ThrowIfGreaterThan(tag.GetUtf8Length(), Maximum.TagLengthInBytes);
                ArgumentOutOfRangeException.ThrowIfGreaterThan(tag.GetGraphemeLength(), Maximum.TagLengthInGraphemes);
            }
        }

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await Quote(
            strongReference: strongReference,
            images: [image],
            tags: tags,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a quote post with an image using the specified <paramref name="rKey"/>.
    /// </summary>
    /// <param name="rKey">The record key to use for the quote post.</param>
    /// <param name="strongReference">A <see cref="StrongReference"/> to the post to be quoted.</param>
    /// <param name="image">An image to attach to the quote post.</param>
    /// <param name="tags">Any tags to apply to the quote post.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="strongReference"/> or <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="tags"/> contains a <see langword="null"/> or empty tag.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="tags"/> has too many tags or a tag exceeds the maximum length.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "The caller-supplied-key overload preserves the existing signature and cancellation-token call patterns.")]
    public async Task<AtProtoHttpResult<CreateRecordResult>> Quote(
        RecordKey? rKey,
        StrongReference strongReference,
        EmbeddedImage image,
        ICollection<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (tags is not null)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(tags.Count, Maximum.TagsInPost);

            foreach (string tag in tags)
            {
                ArgumentException.ThrowIfNullOrEmpty(tag);
                ArgumentOutOfRangeException.ThrowIfGreaterThan(tag.GetUtf8Length(), Maximum.TagLengthInBytes);
                ArgumentOutOfRangeException.ThrowIfGreaterThan(tag.GetGraphemeLength(), Maximum.TagLengthInGraphemes);
            }
        }

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await Quote(
            rKey,
            strongReference,
            images: [image],
            tags: tags,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates an Bluesky post record quoting the post identified by <see cref="StrongReference"/>.
    /// </summary>
    /// <param name="strongReference">A <see cref="StrongReference"/> to the post to be quoted.</param>
    /// <param name="images">Any images to attach to the quote post.</param>
    /// <param name="tags">Any tags to apply to the quote post.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="tags"/> contains a <see langword="null"/> or empty tag.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="strongReference"/> is <see langword="null"/></exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="images"/> has too many images, or <paramref name="tags"/> has too many tags, or a tag that exceeds the maximum length.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the ApplyWrites() result is not as expected.</exception>
    public async Task<AtProtoHttpResult<CreateRecordResult>> Quote(
        StrongReference strongReference,
        ICollection<EmbeddedImage>? images = null,
        ICollection<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        return await Quote(
            rKey: null,
            strongReference: strongReference,
            images: images,
            tags: tags,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a quote post using the specified <paramref name="rKey"/>.
    /// </summary>
    /// <param name="rKey">The record key to use for the quote post.</param>
    /// <param name="strongReference">A <see cref="StrongReference"/> to the post to be quoted.</param>
    /// <param name="images">Any images to attach to the quote post.</param>
    /// <param name="tags">Any tags to apply to the quote post.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="tags"/> contains a <see langword="null"/> or empty tag.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="strongReference"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="images"/> has too many images, or <paramref name="tags"/> has too many tags, or a tag that exceeds the maximum length.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the ApplyWrites() result is not as expected.</exception>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "The caller-supplied-key overload preserves the existing signature and cancellation-token call patterns.")]
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to ApplyWrites().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to ApplyWrites().")]
    public async Task<AtProtoHttpResult<CreateRecordResult>> Quote(
        RecordKey? rKey,
        StrongReference strongReference,
        ICollection<EmbeddedImage>? images = null,
        ICollection<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(strongReference);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        if (images is not null)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(images.Count, Maximum.ImagesInPost);
        }

        if (tags is not null)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(tags.Count, Maximum.TagsInPost);
            foreach (string tag in tags)
            {
                ArgumentException.ThrowIfNullOrEmpty(tag);
                ArgumentOutOfRangeException.ThrowIfGreaterThan(tag.GetUtf8Length(), Maximum.TagLengthInBytes);
                ArgumentOutOfRangeException.ThrowIfGreaterThan(tag.GetGraphemeLength(), Maximum.TagLengthInGraphemes);
            }
        }

        Post postRecord = new()
        {
            EmbeddedRecord = new EmbeddedRecord(strongReference),
            Text = string.Empty,
            CreatedAt = DateTimeOffset.UtcNow,
            Tags = tags
        };

        if (images is not null)
        {
            postRecord.EmbeddedRecord =
                new EmbeddedRecordWithMedia(new EmbeddedRecord(strongReference), new EmbeddedImages(images));
        }

        RecordKey postRecordKey = rKey ?? TimestampIdentifier.Next();
        CreateOperation createOperation = new(CollectionNsid.Post, postRecordKey, postRecord);

        AtProtoHttpResult<ApplyWritesResults> result = await ApplyWrites(
            operations: [createOperation],
            jsonSerializerOptions: BlueskyServer.BlueskyJsonSerializerOptions,
            repo: Did,
            cid: null,
            validate: true,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (result.Succeeded)
        {
            if (result.Result.Results.Count == 0 ||
                result.Result.Results.Count > 1)
            {
                Logger.QuoteCreateSucceededButResultResultsIsNotCountOne(_logger, result.Result.Results.Count);
                throw new InvalidOperationException($"ApplyWrites() returned a results array with a count of {result.Result.Results.Count}");
            }

            if (result.Result.Results.First() is not ApplyWritesCreateResult recordResult)
            {
                Logger.QuoteCreateSucceededButReturnResultUnexpectedType(_logger, result.Result.Results.First().GetType());
                throw new InvalidOperationException("ApplyWrites() result was not of type ApplyWritesCreateResult.");
            }

            return new AtProtoHttpResult<CreateRecordResult>(
                new CreateRecordResult(
                    recordResult.Uri,
                    recordResult.Cid,
                    result.Result.Commit,
                    recordResult.ValidationStatus),
                result.StatusCode,
                result.HttpResponseHeaders,
                result.AtErrorDetail,
                result.RateLimit);
        }

        return new AtProtoHttpResult<CreateRecordResult>(
            null,
            result.StatusCode,
            result.HttpResponseHeaders,
            result.AtErrorDetail,
            result.RateLimit);
    }

    private Task<AtProtoHttpResult<CreateRecordResult>> QuoteWithMedia(
        RecordKey? rKey,
        StrongReference strongReference,
        string text,
        EmbeddedBase media,
        ICollection<string>? tags,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(strongReference);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(media);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        Post post = new(
            text,
            DateTimeOffset.UtcNow,
            langs: [Thread.CurrentThread.CurrentUICulture.Name],
            embeddedRecord: new EmbeddedRecordWithMedia(new EmbeddedRecord(strongReference), media),
            tags: tags);

        return Post(rKey, post, cancellationToken: cancellationToken);
    }
}
