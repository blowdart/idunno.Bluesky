// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Actor;
using idunno.Bluesky.Embed;
using idunno.Bluesky.Feed.Gates;
using idunno.Bluesky.Record;
using idunno.Bluesky.RichText;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Creates a Bluesky post record.
    /// </summary>
    /// <param name="text">The text of the post record to create.</param>
    /// <param name="langs">The languages the post was written in.</param>
    /// <param name="createdAt">The <see cref="DateTimeOffset"/> the post was created at.</param>
    /// <param name="threadGateRules">Thread gating rules to apply to the post, if any. Only valid if the post is a thread root.</param>
    /// <param name="postGateRules">Post gating rules to apply to the post, if any.</param>
    /// <param name="interactionPreferences">The user's default interaction preferences. This will take effect if <paramref name="threadGateRules"/> and/or <paramref name="postGateRules"/> is <see langword="null"/>.</param>
    /// <param name="labels">Optional self label settings for the post media content.</param>
    /// <param name="tags">Any optional tags to apply to the post.</param>
    /// <param name="extractFacets">Flag indicating whether facets should be extracted from <paramref name="text"/>.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="text"/> is <see langword="null"/>, empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="text"/> length is greater than the maximum number of characters or graphemes.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Various helpers")]
    public async Task<AtProtoHttpResult<CreateRecordResult>> Post(
        string text,
        string langs,
        DateTimeOffset? createdAt = null,
        ICollection<ThreadGateRule>? threadGateRules = null,
        ICollection<PostGateRule>? postGateRules = null,
        PostInteractionSettingsPreferences? interactionPreferences = null,
        PostSelfLabels? labels = null,
        ICollection<string>? tags = null,
        bool extractFacets = true,
        CancellationToken cancellationToken = default)
    {
        string[]? langsArray = null;

        if (langs is not null)
        {
            langsArray = [langs];
        }

        return await Post(
            text,
            createdAt,
            langsArray,
            threadGateRules,
            postGateRules,
            interactionPreferences,
            labels,
            tags,
            extractFacets,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a Bluesky post record.
    /// </summary>
    /// <param name="text">The text of the post record to create.</param>
    /// <param name="createdAt">The <see cref="DateTimeOffset"/> the post was created at.</param>
    /// <param name="langs">The languages the post was written in.</param>
    /// <param name="threadGateRules">Thread gating rules to apply to the post, if any. Only valid if the post is a thread root.</param>
    /// <param name="postGateRules">Post gating rules to apply to the post, if any.</param>
    /// <param name="interactionPreferences">The user's default interaction preferences. This will take effect if <paramref name="threadGateRules"/> and/or <paramref name="postGateRules"/> is <see langword="null"/>.</param>
    /// <param name="labels">Optional self label settings for the post media content.</param>
    /// <param name="tags">Any optional tags to apply to the post.</param>
    /// <param name="extractFacets">Flag indicating whether facets should be extracted from <paramref name="text"/>.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="text"/> is <see langword="null"/>, empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="text"/> length is greater than the maximum number of characters or graphemes.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Various helpers")]
    public async Task<AtProtoHttpResult<CreateRecordResult>> Post(
        string text,
        DateTimeOffset? createdAt = null,
        ICollection<string>? langs = null,
        ICollection<ThreadGateRule>? threadGateRules = null,
        ICollection<PostGateRule>? postGateRules = null,
        PostInteractionSettingsPreferences? interactionPreferences = null,
        PostSelfLabels? labels = null,
        ICollection<string>? tags = null,
        bool extractFacets = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        if (text.Length > Maximum.PostLengthInCharacters || text.GetGraphemeLength() > Maximum.PostLengthInGraphemes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(text),
                $"text cannot have a be longer than than {Maximum.PostLengthInCharacters} characters, or {Maximum.PostLengthInGraphemes} graphemes.");
        }

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        if (threadGateRules is null && interactionPreferences is not null)
        {
            threadGateRules = interactionPreferences.ThreadGateAllowRules;
        }

        if (postGateRules is null && interactionPreferences is not null)
        {
            postGateRules = interactionPreferences.PostGateEmbeddingRules;
        }

        return await Post(
            text,
            images: null,
            createdAt: createdAt,
            langs: langs,
            threadGateRules: threadGateRules,
            postGateRules: postGateRules,
            interactionPreferences: interactionPreferences,
            labels: labels,
            tags: tags,
            extractFacets: extractFacets,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a Bluesky post record with an image.
    /// </summary>
    /// <param name="text">The text of the post record to create.</param>
    /// <param name="image">The image to attach to the post.</param>
    /// <param name="createdAt">The <see cref="DateTimeOffset"/> the post was created at.</param>
    /// <param name="langs">The languages the post was written in.</param>
    /// <param name="threadGateRules">Thread gating rules to apply to the post, if any. Only valid if the post is a thread root.</param>
    /// <param name="postGateRules">Post gating rules to apply to the post, if any.</param>
    /// <param name="interactionPreferences">The user's default interaction preferences. This will take effect if <paramref name="threadGateRules"/> and/or <paramref name="postGateRules"/> is <see langword="null"/>.</param>
    /// <param name="labels">Optional self label settings for the post media content.</param>
    /// <param name="tags">Any optional tags to apply to the post.</param>
    /// <param name="extractFacets">Flag indicating whether facets should be extracted from <paramref name="text" />.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="text"/> is <see langword="null"/>, empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="text"/> length is greater than the maximum number of characters or graphemes.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Various helpers")]
    public async Task<AtProtoHttpResult<CreateRecordResult>> Post(
        string text,
        EmbeddedImage image,
        DateTimeOffset? createdAt = null,
        ICollection<string>? langs = null,
        ICollection<ThreadGateRule>? threadGateRules = null,
        ICollection<PostGateRule>? postGateRules = null,
        PostInteractionSettingsPreferences? interactionPreferences = null,
        PostSelfLabels? labels = null,
        ICollection<string>? tags = null,
        bool extractFacets = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        if (text.Length > Maximum.PostLengthInCharacters || text.GetGraphemeLength() > Maximum.PostLengthInGraphemes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(text),
                $"text cannot have a be longer than than {Maximum.PostLengthInCharacters} characters, or {Maximum.PostLengthInGraphemes} graphemes.");
        }

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        List<EmbeddedImage>? images = null;

        if (image is not null)
        {
            images = [image];
        }

        return await Post(
            text,
            images: images,
            createdAt: createdAt,
            langs: langs,
            threadGateRules: threadGateRules,
            postGateRules: postGateRules,
            interactionPreferences: interactionPreferences,
            labels: labels,
            tags: tags,
            extractFacets: extractFacets,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a Bluesky post record. with multiple images.
    /// </summary>
    /// <param name="text">The text of the post record to create.</param>
    /// <param name="images">Any images to attach to the post.</param>
    /// <param name="createdAt">The <see cref="DateTimeOffset"/> the post was created at.</param>
    /// <param name="langs">The languages the post was written in.</param>
    /// <param name="threadGateRules">Any thread gating rules to apply to the post, if any. Only valid if the post is a thread root.</param>
    /// <param name="postGateRules">Any post gating rules to apply to the post, if any.</param>
    /// <param name="interactionPreferences">Any default interaction preferences. This will take effect if <paramref name="threadGateRules"/> and/or <paramref name="postGateRules"/> is <see langword="null"/>.</param>
    /// <param name="labels">Optional self label settings for the post media content.</param>
    /// <param name="tags">Any optional tags to apply to the post.</param>
    /// <param name="extractFacets">Flag indicating whether facets should be extracted from <paramref name="text" />.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="text"/> is <see langword="null"/>, empty or whitespace.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   if <paramref name="text"/> length is greater than the maximum number of characters or graphemes, or
    ///   <paramref name="images"/> contains more than the maximum allowed number of images.
    /// </exception>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Various helpers")]
    public async Task<AtProtoHttpResult<CreateRecordResult>> Post(
        string text,
        ICollection<EmbeddedImage>? images,
        DateTimeOffset? createdAt = null,
        ICollection<string>? langs = null,
        ICollection<ThreadGateRule>? threadGateRules = null,
        ICollection<PostGateRule>? postGateRules = null,
        PostInteractionSettingsPreferences? interactionPreferences = null,
        PostSelfLabels? labels = null,
        ICollection<string>? tags = null,
        bool extractFacets = true,
        CancellationToken cancellationToken = default)
    {
        EmbeddedImages? embeddedImages = null;

        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        if (text.Length > Maximum.PostLengthInCharacters || text.GetGraphemeLength() > Maximum.PostLengthInGraphemes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(text),
                $"text cannot have a be longer than than {Maximum.PostLengthInCharacters} characters, or {Maximum.PostLengthInGraphemes} graphemes.");
        }

        if (images != null)
        {
            if (images.Count > Maximum.ImagesInPost)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(images),
                    $"Cannot have more than {Maximum.ImagesInPost} images.");
            }
            else
            {
                embeddedImages = new EmbeddedImages(images);
            }
        }

        if (threadGateRules != null && threadGateRules.Count > Maximum.ThreadGateRules)
        {
            throw new ArgumentOutOfRangeException(nameof(threadGateRules), $"Cannot have more than {Maximum.ThreadGateRules} rules.");
        }

        if (postGateRules != null && postGateRules.Count > Maximum.PostGateRules)
        {
            throw new ArgumentOutOfRangeException(nameof(postGateRules), $"Cannot have more than {Maximum.PostGateRules} rules.");
        }

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        DateTimeOffset creationDateTime = DateTimeOffset.UtcNow;
        if (createdAt is not null)
        {
            creationDateTime = createdAt.Value.ToUniversalTime();
        }

        Post post = new(
            text,
            createdAt: creationDateTime,
            langs: langs,
            embeddedRecord: embeddedImages,
            tags: tags);

        if (extractFacets)
        {
            IList<Facet> extractedFacets = await FacetExtractor.ExtractFacets(text, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (extractedFacets.Any())
            {
                post.Facets = extractedFacets;
            }
        }

        if (labels is not null)
        {
            post.SetSelfLabels(labels);
        }

        return await CreatePost(
            post,
            threadGateRules: threadGateRules,
            postGateRules: postGateRules,
            interactionPreferences: interactionPreferences,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a Bluesky post record with a video
    /// </summary>
    /// <param name="text">The text of the post record to create.</param>
    /// <param name="video">The video to embed in the post.</param>
    /// <param name="createdAt">The <see cref="DateTimeOffset"/> the post was created at.</param>
    /// <param name="langs">The languages the post was written in.</param>
    /// <param name="threadGateRules">Thread gating rules to apply to the post, if any. Only valid if the post is a thread root.</param>
    /// <param name="postGateRules">Post gating rules to apply to the post, if any.</param>
    /// <param name="interactionPreferences">The user's default interaction preferences. This will take effect if <paramref name="threadGateRules"/> and/or <paramref name="postGateRules"/> is <see langword="null"/>.</param>
    /// <param name="labels">Any optional self label settings for the post media content.</param>
    /// <param name="tags">Any optional tags to apply to the post.</param>
    /// <param name="extractFacets">Flag indicating whether facets should be extracted from <paramref name="text" />.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="video"/> is <see langword="null"/></exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when
    /// <paramref name="text"/> length is greater than the maximum number of characters or graphemes, or
    /// <paramref name="threadGateRules"/> contains more than the maximum allowed number of rules, or
    /// <paramref name="postGateRules"/> contains more than the maximum allowed number of rules
    /// </exception>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Various helpers")]
    public async Task<AtProtoHttpResult<CreateRecordResult>> Post(
        string? text,
        EmbeddedVideo video,
        DateTimeOffset? createdAt = null,
        ICollection<string>? langs = null,
        ICollection<ThreadGateRule>? threadGateRules = null,
        ICollection<PostGateRule>? postGateRules = null,
        PostInteractionSettingsPreferences? interactionPreferences = null,
        PostSelfLabels? labels = null,
        ICollection<string>? tags = null,
        bool extractFacets = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(video);

        if (text is not null &&
            (text.Length > Maximum.PostLengthInCharacters || text.GetGraphemeLength() > Maximum.PostLengthInGraphemes))
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(text.Length, Maximum.PostLengthInCharacters);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(text.GetGraphemeLength(), Maximum.PostLengthInGraphemes);
        }

        if (threadGateRules is not null && threadGateRules.Count > Maximum.ThreadGateRules)
        {
            throw new ArgumentOutOfRangeException(nameof(threadGateRules), $"Cannot have more than {Maximum.ThreadGateRules} rules.");
        }

        if (postGateRules is not null && postGateRules.Count > Maximum.PostGateRules)
        {
            throw new ArgumentOutOfRangeException(nameof(postGateRules), $"Cannot have more than {Maximum.PostGateRules} rules.");
        }

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        DateTimeOffset creationDateTime = DateTimeOffset.UtcNow;
        if (createdAt is not null)
        {
            creationDateTime = createdAt.Value.ToUniversalTime();
        }

        Post post = new(
            text,
            createdAt: creationDateTime,
            langs: langs,
            embeddedRecord: video,
            tags: tags);

        if (extractFacets && text is not null)
        {
            IList<Facet> extractedFacets = await FacetExtractor.ExtractFacets(text, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (extractedFacets.Any())
            {
                post.Facets = extractedFacets;
            }
        }

        if (labels is not null)
        {
            post.SetSelfLabels(labels);
        }

        return await CreatePost(
            post,
            threadGateRules: threadGateRules,
            postGateRules: postGateRules,
            interactionPreferences: interactionPreferences,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a Bluesky post record containing just a external Open Graph embedded card.
    /// </summary>
    /// <param name="externalCard">An Open Graph embedded card.</param>
    /// <param name="createdAt">The <see cref="DateTimeOffset"/> the post was created at.</param>
    /// <param name="langs">The languages the post was written in.</param>
    /// <param name="threadGateRules">Thread gating rules to apply to the post, if any. Only valid if the post is a thread root.</param>
    /// <param name="postGateRules">Post gating rules to apply to the post, if any.</param>
    /// <param name="interactionPreferences">The user's default interaction preferences. This will take effect if <paramref name="threadGateRules"/> and/or <paramref name="postGateRules"/> is <see langword="null"/>.</param>
    /// <param name="labels">Optional self label settings for the post media content.</param>
    /// <param name="tags">Any optional tags to apply to the post.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="externalCard"/> is <see langword="null"/>.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    /// <remarks><para>Posts containing an embedded card do not require post text.</para></remarks>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Various helpers")]
    public async Task<AtProtoHttpResult<CreateRecordResult>> Post(
        EmbeddedExternal externalCard,
        DateTimeOffset? createdAt = null,
        ICollection<string>? langs = null,
        ICollection<ThreadGateRule>? threadGateRules = null,
        ICollection<PostGateRule>? postGateRules = null,
        PostInteractionSettingsPreferences? interactionPreferences = null,
        PostSelfLabels? labels = null,
        ICollection<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(externalCard);

        return await Post(
            text: string.Empty,
            externalCard: externalCard,
            createdAt: createdAt,
            langs: langs,
            threadGateRules: threadGateRules,
            postGateRules: postGateRules,
            interactionPreferences: interactionPreferences,
            extractFacets: false,
            labels: labels,
            tags: tags,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a Bluesky post record containing text and an external Open Graph embedded card.
    /// </summary>
    /// <param name="text">The text of the post record to create.</param>
    /// <param name="externalCard">An Open Graph embedded card.</param>
    /// <param name="createdAt">The <see cref="DateTimeOffset"/> the post was created at.</param>
    /// <param name="langs">The languages the post was written in.</param>
    /// <param name="threadGateRules">Thread gating rules to apply to the post, if any. Only valid if the post is a thread root.</param>
    /// <param name="postGateRules">Post gating rules to apply to the post, if any.</param>
    /// <param name="interactionPreferences">The user's default interaction preferences. This will take effect if <paramref name="threadGateRules"/> and/or <paramref name="postGateRules"/> is <see langword="null"/>.</param>
    /// <param name="labels">Optional self label settings for the post media content.</param>
    /// <param name="extractFacets">Flag indicating whether facets should be extracted from <paramref name="text" />.</param>
    /// <param name="tags">Optional collection of tags to apply to the post.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="externalCard"/> is <see langword="null"/>, empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="text"/> length is greater than the maximum number of characters or graphemes.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Various helpers")]
    public async Task<AtProtoHttpResult<CreateRecordResult>> Post(
        string text,
        EmbeddedExternal externalCard,
        DateTimeOffset? createdAt = null,
        ICollection<string>? langs = null,
        ICollection<ThreadGateRule>? threadGateRules = null,
        ICollection<PostGateRule>? postGateRules = null,
        PostInteractionSettingsPreferences? interactionPreferences = null,
        PostSelfLabels? labels = null,
        bool extractFacets = true,
        ICollection<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(externalCard);

        if (text.Length > Maximum.PostLengthInCharacters || text.GetGraphemeLength() > Maximum.PostLengthInGraphemes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(text),
                $"text cannot have a be longer than than {Maximum.PostLengthInCharacters} characters, or {Maximum.PostLengthInGraphemes} graphemes.");
        }

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        IList<Facet>? facets = null;

        if (extractFacets)
        {
            facets = await FacetExtractor.ExtractFacets(text, cancellationToken).ConfigureAwait(false);
        }

        var postBuilder = new PostBuilder(text, createdAt: createdAt, langs: langs, facets: facets, tags: tags);

        postBuilder.EmbedRecord(externalCard);

        if (interactionPreferences is not null)
        {
            postBuilder.ApplyInteractionPreferences(interactionPreferences);
        }

        if (threadGateRules is not null)
        {
            postBuilder.ThreadGateRules = [.. threadGateRules];
        }

        if (postGateRules is not null)
        {
            postBuilder.PostGateRules = [.. postGateRules];
        }

        if (labels is not null)
        {
            postBuilder.SetSelfLabels(labels);
        }

        return await Post(postBuilder, cancellationToken: cancellationToken).ConfigureAwait(false);
    }


    /// <summary>
    /// Creates a post record from the specified <paramref name="post"/>.
    /// </summary>
    /// <param name="post">The post to create the record from.</param>
    /// <param name="threadGateRules">Thread gating rules to apply to the post, if any. Only valid if the post is a thread root.</param>
    /// <param name="postGateRules">Gating rules to apply to the <paramref name="post"/>, if any.</param>
    /// <param name="interactionPreferences">The user's default interaction preferences. This will take effect if <paramref name="threadGateRules"/> and/or <paramref name="postGateRules"/> is <see langword="null"/>.</param>
    /// <param name="extractFacets">Flag indicating whether facets should be extracted from the post text.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="post"/> is <see langword="null"/>.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    [SuppressMessage("Minor Code Smell", "S3267:Loops should be simplified with \"LINQ\" expressions", Justification = "Remove linq for clarity")]
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Various helpers")]
    public async Task<AtProtoHttpResult<CreateRecordResult>> Post(
        Post post,
        ICollection<ThreadGateRule>? threadGateRules = null,
        ICollection<PostGateRule>? postGateRules = null,
        PostInteractionSettingsPreferences? interactionPreferences = null,
        bool extractFacets = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(post);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        if (!string.IsNullOrEmpty(post.Text) && extractFacets)
        {
            IList<Facet>? facets = await FacetExtractor.ExtractFacets(post.Text, cancellationToken).ConfigureAwait(false);
            if (facets is not null && facets.Count > 0)
            {
                if (post.Facets == null || post.Facets.Count == 0)
                {
                    post.Facets = facets;
                }
                else
                {
                    if (post.Facets.IsReadOnly)
                    {
                        post.Facets = [.. post.Facets];
                    }

                    foreach (Facet? facet in facets)
                    {
                        if (facet is not null)
                        {
                            bool matchingFacetLocationFound = false;
                            foreach (Facet? existingFacet in post.Facets)
                            {
                                if (existingFacet is not null &&
                                    existingFacet.Index.ByteStart == facet.Index.ByteStart &&
                                    existingFacet.Index.ByteEnd == facet.Index.ByteEnd &&
                                    existingFacet.Features.SequenceEqual(facet.Features))
                                {
                                    matchingFacetLocationFound = true;
                                }
                            }

                            if (!matchingFacetLocationFound)
                            {
                                post.Facets.Add(facet);
                            }
                        }
                    }
                }
            }
        }

        AtProtoHttpResult<CreateRecordResult> result = await CreatePost(
            post,
            threadGateRules: threadGateRules,
            postGateRules: postGateRules,
            interactionPreferences: interactionPreferences,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (result.Succeeded)
        {
            Logger.CreatePostWithPostSucceeded(_logger, Did, result.Result.Uri, result.Result.Cid);
        }
        else
        {
            Logger.CreatePostWithPostFailed(_logger, result.StatusCode, Did, result.AtErrorDetail?.Error, result.AtErrorDetail?.Message);
        }

        return result;
    }

    /// <summary>
    /// Creates a Bluesky post record from the specified <paramref name="postBuilder"/>.
    /// </summary>
    /// <param name="postBuilder">The <see cref="PostBuilder"/> to use to create the record.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="postBuilder"/> is <see langword="null"/>.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Various helpers")]
    public async Task<AtProtoHttpResult<CreateRecordResult>> Post(PostBuilder postBuilder, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(postBuilder);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        Post post;
        List<ThreadGateRule>? threadGateRules = null;
        List<PostGateRule>? postGateRules = null;

        lock (postBuilder)
        {
            post = postBuilder.ToPost();

            // The post builder already did the work in taking the default gating preferences and applying them.

            if (postBuilder.ThreadGateRules is not null)
            {
                threadGateRules = [.. postBuilder.ThreadGateRules];
            }

            if (postBuilder.PostGateRules is not null)
            {
                postGateRules = [.. postBuilder.PostGateRules];
            }
        }

        return await CreatePost(
            post,
            threadGateRules: threadGateRules,
            postGateRules: postGateRules,
            interactionPreferences: null,
            cancellationToken).ConfigureAwait(false);
    }

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to CreateRecord().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to CreateRecord().")]
    private async Task<AtProtoHttpResult<CreateRecordResult>> CreatePost(
        Post post,
        ICollection<ThreadGateRule>? threadGateRules,
        ICollection<PostGateRule>? postGateRules,
        PostInteractionSettingsPreferences? interactionPreferences,
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        if (threadGateRules is null && postGateRules is null && interactionPreferences is null && !string.IsNullOrEmpty(post.Text))
        {
            // We use the BlueskyTimestampedRecordValue class as the generic so the type discriminator appears in the serialized output.
            return await CreateRecord<BlueskyTimestampedRecord>(
                record: post,
                jsonSerializerOptions: BlueskyServer.BlueskyJsonSerializerOptions,
                collection: CollectionNsid.Post,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // If a post has no text (which is possible if there are embedded records
            // or it has gates and creating the post and gates need to be atomic
            // we have to use ApplyWrites() rather than CreateRecord()
            List<WriteOperation> writeRequests = [];

            // We need to generate a record key to hang it all together.
            RecordKey rKey = TimestampIdentifier.Next();
            AtUri postUri = new($"at://{Did}/{CollectionNsid.Post}/{rKey}");

            writeRequests.Add(new CreateOperation(CollectionNsid.Post, rKey, post));

            if (threadGateRules is null && interactionPreferences is not null)
            {
                threadGateRules = interactionPreferences.ThreadGateAllowRules;
            }

            if (postGateRules is null && interactionPreferences is not null)
            {
                postGateRules = interactionPreferences.PostGateEmbeddingRules;
            }

            if (threadGateRules is not null)
            {
                writeRequests.Add(new CreateOperation(
                    CollectionNsid.ThreadGate,
                    rKey,
                    new ThreadGate(postUri, threadGateRules)));
            }

            if (postGateRules is not null)
            {
                writeRequests.Add(new CreateOperation(
                    CollectionNsid.PostGate,
                    rKey,
                    new PostGate(postUri, postGateRules)));
            }

            AtProtoHttpResult<ApplyWritesResults> response =
                await ApplyWrites(
                    writeRequests,
                    jsonSerializerOptions: BlueskyServer.BlueskyJsonSerializerOptions,
                    repo: Did,
                    validate: true,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                Logger.CreatePostWithGatesSucceeded(_logger, rKey, Did);

                CreateRecordResult? createRecordResult = null;

                foreach (IApplyWritesResult result in response.Result.Results)
                {
                    if (result is ApplyWritesCreateResult createResult && createResult.Uri == postUri)
                    {
                        createRecordResult = new CreateRecordResult(postUri, createResult.Cid, validationStatus: createResult.ValidationStatus, commit: response.Result.Commit);
                        break;
                    }
                }

                return new AtProtoHttpResult<CreateRecordResult>(createRecordResult, response.StatusCode, response.HttpResponseHeaders, response.AtErrorDetail, response.RateLimit);
            }
            else
            {
                Logger.CreatePostWithGatesFailed(_logger, response.StatusCode, Did, response.AtErrorDetail?.Error, response.AtErrorDetail?.Message);
                return new AtProtoHttpResult<CreateRecordResult>(null, response.StatusCode, response.HttpResponseHeaders, response.AtErrorDetail, response.RateLimit);
            }
        }
    }

}
