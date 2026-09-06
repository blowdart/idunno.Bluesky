// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Embed;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Creates a simple Bluesky post record with the specified <paramref name="text"/>, in reply to the <paramref name="parent"/> post.
    /// </summary>
    /// <param name="parent">A <see cref="StrongReference"/> to the parent post that the new post will be in reply to.</param>
    /// <param name="text">The text for the new reply.</param>
    /// <param name="tags">Any tags to apply to the reply.</param>
    /// <param name="extractFacets">Flag indicating whether facets should be extracted from the post text automatically.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="parent"/> or <paramref name="text"/> is nul.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="text"/>'s length is greater than the maximum allowed characters or graphemes.</exception>
    public async Task<AtProtoHttpResult<CreateRecordResult>> ReplyTo(
        StrongReference parent,
        string text,
        ICollection<string>? tags = null,
        bool extractFacets = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(text.Length, Maximum.PostLengthInCharacters);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(text.GetGraphemeLength(), Maximum.PostLengthInCharacters);

        ArgumentNullException.ThrowIfNull(parent);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await InternalReplyTo(
            parent: parent,
            text: text,
            images: null,
            tags: tags,
            extractFacets: extractFacets,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a simple Bluesky post record with the specified <paramref name="text"/>, in reply to the <paramref name="parent"/> post.
    /// </summary>
    /// <param name="parent">A <see cref="StrongReference"/> to the parent post that the new post will be in reply to.</param>
    /// <param name="text">The text for the new reply</param>
    /// <param name="image">An image to attach to the reply.</param>
    /// <param name="tags">Any tags to apply to the reply.</param>
    /// <param name="extractFacets">Flag indicating whether facets should be extracted from the post text automatically.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="tags"/> contains a <see langword="null"/> or empty tag.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="parent"/>, <paramref name="text"/> or <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   Thrown when <paramref name="text"/>'s length is greater than the maximum allowed characters or graphemes,
    ///   or <paramref name="tags"/> contains a tag whose length is greater than the maximum allowed characters or graphemes.
    /// </exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<CreateRecordResult>> ReplyTo(
        StrongReference parent,
        string text,
        EmbeddedImage image,
        ICollection<string>? tags = null,
        bool extractFacets = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(text.Length, Maximum.PostLengthInCharacters);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(text.GetGraphemeLength(), Maximum.PostLengthInCharacters);

        ArgumentNullException.ThrowIfNull(parent);
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

        List<EmbeddedImage> images = [image];

        return await InternalReplyTo(
            parent: parent,
            text: text,
            images: images,
            tags: tags,
            extractFacets: extractFacets,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a simple Bluesky post record with the specified <paramref name="text"/> in reply to the <paramref name="parent"/> post.
    /// </summary>
    /// <param name="parent">A <see cref="StrongReference"/> to the parent post that the new post will be in reply to.</param>
    /// <param name="text">The text for the new post</param>
    /// <param name="images">Any images to attach to the post.</param>
    /// <param name="tags">Any tags to apply to the reply.</param>
    /// <param name="extractFacets">Flag indicating whether facets should be extracted from the post text automatically.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown <paramref name="text"/> is <see langword="null"/> or empty, or <paramref name="tags"/> contains a <see langword="null"/> or empty tag.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="parent"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   Thrown when <paramref name="text"/>'s length is greater than the maximum allowed characters or graphemes, or
    ///   <paramref name="tags"/> contains a tag whose length is greater than the maximum allowed characters or graphemes.
    /// </exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<CreateRecordResult>> ReplyTo(
        StrongReference parent,
        string text,
        ICollection<EmbeddedImage> images,
        ICollection<string>? tags = null,
        bool extractFacets = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(text.Length, Maximum.PostLengthInCharacters);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(text.GetGraphemeLength(), Maximum.PostLengthInCharacters);
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(images);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(images.Count, Maximum.ImagesInPost);

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

        return await InternalReplyTo(
            parent: parent,
            text: text,
            images: images,
            tags: tags,
            extractFacets: extractFacets,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async Task<AtProtoHttpResult<CreateRecordResult>> InternalReplyTo(
        StrongReference parent,
        string text,
        ICollection<EmbeddedImage>? images = null,
        ICollection<string>? tags = null,
        bool extractFacets = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(text.Length, Maximum.PostLengthInCharacters);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(text.GetGraphemeLength(), Maximum.PostLengthInCharacters);
        ArgumentNullException.ThrowIfNull(parent);

        if (images != null)
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

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        AtProtoHttpResult<ReplyReferences> replyReferencesResult = await GetReplyReferences(parent, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!replyReferencesResult.Succeeded)
        {
            return new AtProtoHttpResult<CreateRecordResult>(
                null,
                replyReferencesResult.StatusCode,
                replyReferencesResult.HttpResponseHeaders,
                replyReferencesResult.AtErrorDetail,
                replyReferencesResult.RateLimit);
        }

        PostBuilder postBuilder = new(text: text, langs: null, createdAt: null, labels: null, tags: tags)
        {
            InReplyTo = replyReferencesResult.Result,
        };

        if (images is not null)
        {
            postBuilder.Add(images);
        }

        if (extractFacets)
        {
            await postBuilder.ExtractFacets(
                facetExtractor: FacetExtractor,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        return await Post(postBuilder, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
