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
    /// <param name="extractFacets"><see langword="true"/> to extract facets from the reply text automatically (the default); otherwise, <see langword="false"/>.</param>
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
        ArgumentOutOfRangeException.ThrowIfGreaterThan(text.GetUtf8Length(), Maximum.PostLengthInBytes);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(text.GetGraphemeLength(), Maximum.PostLengthInGraphemes);

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
    /// Creates a reply post using the specified <paramref name="rKey"/>.
    /// </summary>
    /// <param name="rKey">The record key to use for the reply post and any associated gate records.</param>
    /// <param name="parent">A <see cref="StrongReference"/> to the parent post that the new post will be in reply to.</param>
    /// <param name="text">The text for the new reply.</param>
    /// <param name="tags">Any tags to apply to the reply.</param>
    /// <param name="extractFacets"><see langword="true"/> to extract facets from the reply text automatically (the default); otherwise, <see langword="false"/>.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="parent"/> or <paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="text"/>'s length is greater than the maximum allowed characters or graphemes.</exception>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "The caller-supplied-key overload preserves the existing signature and cancellation-token call patterns.")]
    public async Task<AtProtoHttpResult<CreateRecordResult>> ReplyTo(
        RecordKey? rKey,
        StrongReference parent,
        string text,
        ICollection<string>? tags = null,
        bool extractFacets = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(text.GetUtf8Length(), Maximum.PostLengthInBytes);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(text.GetGraphemeLength(), Maximum.PostLengthInGraphemes);
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
            rKey: rKey,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a simple Bluesky post record with the specified <paramref name="text"/>, in reply to the <paramref name="parent"/> post.
    /// </summary>
    /// <param name="parent">A <see cref="StrongReference"/> to the parent post that the new post will be in reply to.</param>
    /// <param name="text">The text for the new reply</param>
    /// <param name="image">An image to attach to the reply.</param>
    /// <param name="tags">Any tags to apply to the reply.</param>
    /// <param name="extractFacets"><see langword="true"/> to extract facets from the reply text automatically (the default); otherwise, <see langword="false"/>.</param>
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
        ArgumentOutOfRangeException.ThrowIfGreaterThan(text.GetUtf8Length(), Maximum.PostLengthInBytes);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(text.GetGraphemeLength(), Maximum.PostLengthInGraphemes);

        ArgumentNullException.ThrowIfNull(parent);
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
    /// Creates a reply post with an image using the specified <paramref name="rKey"/>.
    /// </summary>
    /// <param name="rKey">The record key to use for the reply post and any associated gate records.</param>
    /// <param name="parent">A <see cref="StrongReference"/> to the parent post that the new post will be in reply to.</param>
    /// <param name="text">The text for the new reply.</param>
    /// <param name="image">An image to attach to the reply.</param>
    /// <param name="tags">Any tags to apply to the reply.</param>
    /// <param name="extractFacets"><see langword="true"/> to extract facets from the reply text automatically (the default); otherwise, <see langword="false"/>.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="tags"/> contains a <see langword="null"/> or empty tag.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="parent"/>, <paramref name="text"/> or <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the text, tags, or image exceeds its supported limit.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "The caller-supplied-key overload preserves the existing signature and cancellation-token call patterns.")]
    public async Task<AtProtoHttpResult<CreateRecordResult>> ReplyTo(
        RecordKey? rKey,
        StrongReference parent,
        string text,
        EmbeddedImage image,
        ICollection<string>? tags = null,
        bool extractFacets = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(parent);
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

        return await InternalReplyTo(
            parent,
            text,
            images: [image],
            tags: tags,
            extractFacets: extractFacets,
            rKey: rKey,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a simple Bluesky post record with the specified <paramref name="text"/> in reply to the <paramref name="parent"/> post.
    /// </summary>
    /// <param name="parent">A <see cref="StrongReference"/> to the parent post that the new post will be in reply to.</param>
    /// <param name="text">The text for the new post</param>
    /// <param name="images">Any images to attach to the post.</param>
    /// <param name="tags">Any tags to apply to the reply.</param>
    /// <param name="extractFacets"><see langword="true"/> to extract facets from the reply text automatically (the default); otherwise, <see langword="false"/>.</param>
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
        ArgumentOutOfRangeException.ThrowIfGreaterThan(text.GetUtf8Length(), Maximum.PostLengthInBytes);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(text.GetGraphemeLength(), Maximum.PostLengthInGraphemes);
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(images);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(images.Count, Maximum.ImagesInPost);

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

        return await InternalReplyTo(
            parent: parent,
            text: text,
            images: images,
            tags: tags,
            extractFacets: extractFacets,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a reply post with images using the specified <paramref name="rKey"/>.
    /// </summary>
    /// <param name="rKey">The record key to use for the reply post and any associated gate records.</param>
    /// <param name="parent">A <see cref="StrongReference"/> to the parent post that the new post will be in reply to.</param>
    /// <param name="text">The text for the new reply.</param>
    /// <param name="images">Any images to attach to the reply.</param>
    /// <param name="tags">Any tags to apply to the reply.</param>
    /// <param name="extractFacets"><see langword="true"/> to extract facets from the reply text automatically (the default); otherwise, <see langword="false"/>.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="tags"/> contains a <see langword="null"/> or empty tag.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="parent"/>, <paramref name="text"/> or <paramref name="images"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the text, image collection, or tags exceed their supported limits.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "The caller-supplied-key overload preserves the existing signature and cancellation-token call patterns.")]
    public async Task<AtProtoHttpResult<CreateRecordResult>> ReplyTo(
        RecordKey? rKey,
        StrongReference parent,
        string text,
        ICollection<EmbeddedImage> images,
        ICollection<string>? tags = null,
        bool extractFacets = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(images);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(images.Count, Maximum.ImagesInPost);

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

        return await InternalReplyTo(
            parent,
            text,
            images: images,
            tags: tags,
            extractFacets: extractFacets,
            rKey: rKey,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async Task<AtProtoHttpResult<CreateRecordResult>> InternalReplyTo(
        StrongReference parent,
        string text,
        ICollection<EmbeddedImage>? images = null,
        ICollection<string>? tags = null,
        bool extractFacets = true,
        RecordKey? rKey = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(text.GetUtf8Length(), Maximum.PostLengthInBytes);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(text.GetGraphemeLength(), Maximum.PostLengthInGraphemes);
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
                ArgumentOutOfRangeException.ThrowIfGreaterThan(tag.GetUtf8Length(), Maximum.TagLengthInBytes);
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

        return await Post(rKey, postBuilder, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
