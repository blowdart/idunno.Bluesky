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
                ArgumentOutOfRangeException.ThrowIfGreaterThan(tag.Length, Maximum.TagLengthInCharacters);
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
                ArgumentOutOfRangeException.ThrowIfGreaterThan(tag.Length, Maximum.TagLengthInCharacters);
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

        if (text.Length > Maximum.PostLengthInCharacters || text.GetGraphemeLength() > Maximum.PostLengthInGraphemes)
        {
            throw new ArgumentOutOfRangeException(nameof(text), $"text cannot have be longer than {Maximum.PostLengthInCharacters} characters, or {Maximum.PostLengthInGraphemes} graphemes.");
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
                ArgumentOutOfRangeException.ThrowIfGreaterThan(tag.Length, Maximum.TagLengthInCharacters);
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
                ArgumentOutOfRangeException.ThrowIfGreaterThan(tag.Length, Maximum.TagLengthInCharacters);
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
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to ApplyWrites().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to ApplyWrites().")]
    public async Task<AtProtoHttpResult<CreateRecordResult>> Quote(
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
                ArgumentOutOfRangeException.ThrowIfGreaterThan(tag.Length, Maximum.TagLengthInCharacters);
                ArgumentOutOfRangeException.ThrowIfGreaterThan(tag.GetGraphemeLength(), Maximum.TagLengthInGraphemes);
            }
        }

        // This is a special case as there is no post text, it cannot go through the normal post APIs, it must go through the repo.ApplyWrites() api.
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

        CreateOperation createOperation = new(CollectionNsid.Post, TimestampIdentifier.Next(), postRecord);

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
                throw new InvalidOperationException($"ApplyWrites() result was not of type ApplyWritesCreateResult.");
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
        else
        {
            return new AtProtoHttpResult<CreateRecordResult>(
                null,
                result.StatusCode,
                result.HttpResponseHeaders,
                result.AtErrorDetail,
                result.RateLimit);
        }
    }
}
