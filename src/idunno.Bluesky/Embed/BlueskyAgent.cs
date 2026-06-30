// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Embed;
using idunno.Bluesky.Feed;

namespace idunno.Bluesky;

partial class BlueskyAgent
{
    /// <summary>
    /// Resolve one or more <see cref="AtUri"/>s into the data needed to render an enhanced external embed.
    /// Returns `associatedRefs` (strongRefs to embed into a post's external.associatedRefs),
    /// the raw `associatedRecords`, and a hydrated `view`.
    /// The response is empty when no records were resolvable, or when validation determined the resolved records don't actually back the requested <paramref name="url"/>;
    /// clients should fall back to their own link-card rendering in that case and skip writing strongRefs to the post.
    /// </summary>
    /// <param name="url">The canonical web URL the embed represents (typically the URL the user pasted into the composer). Used as the returned view's `uri`. May be used for validation in the future.</param>
    /// <param name="uris">An array of AT-URIs to resolve into the data needed for the embed.</param>
    /// <param name="subscribedLabelers">Optional list of subscribed labelers.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when a required argument is <see langword="null" />.</exception>
    public async Task<AtProtoHttpResult<EmbeddedExternalView>> GetEmbedExternalView(
        Uri url,
        AtUri[] uris,
        IEnumerable<Did>? subscribedLabelers = null,
        CancellationToken cancellationToken = default)
    {
        return await BlueskyServer.GetEmbedExternalView(
            url,
            uris,
            AuthenticatedOrUnauthenticatedServiceUri,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            subscribedLabelers: subscribedLabelers,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Resolve one or more <see cref="AtUri"/>s into the data needed to render an enhanced external embed.
    /// Returns `associatedRefs` (strongRefs to embed into a post's external.associatedRefs),
    /// the raw `associatedRecords`, and a hydrated `view`.
    /// The response is empty when no records were resolvable, or when validation determined the resolved records don't actually back the requested URL in the <paramref name="postView"/>;
    /// clients should fall back to their own link-card rendering in that case and skip writing strongRefs to the post.
    /// </summary>
    /// <param name="postView">The <see cref="PostView"/> containing the external embed to resolve.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when a <paramref name="postView"/> or its Embed property is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">Thrown when the <paramref name="postView"/> does not contain a valid <see cref="EmbeddedExternalView"/> or its associated properties.</exception>
    public async Task<AtProtoHttpResult<EmbeddedExternalView>> GetEmbedExternalView(
        PostView postView)
    {
        return await GetEmbedExternalView(postView, null, default).ConfigureAwait(false);
    }

    /// <summary>
    /// Resolve one or more <see cref="AtUri"/>s into the data needed to render an enhanced external embed.
    /// Returns `associatedRefs` (strongRefs to embed into a post's external.associatedRefs),
    /// the raw `associatedRecords`, and a hydrated `view`.
    /// The response is empty when no records were resolvable, or when validation determined the resolved records don't actually back the requested URL in the <paramref name="postView"/>;
    /// clients should fall back to their own link-card rendering in that case and skip writing strongRefs to the post.
    /// </summary>
    /// <param name="postView">The <see cref="PostView"/> containing the external embed to resolve.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when a <paramref name="postView"/> or its Embed property is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">Thrown when the <paramref name="postView"/> does not contain a valid <see cref="EmbeddedExternalView"/> or its associated properties.</exception>
    public async Task<AtProtoHttpResult<EmbeddedExternalView>> GetEmbedExternalView(
        PostView postView,
        CancellationToken cancellationToken)
    {
        return await GetEmbedExternalView(postView, null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Resolve one or more <see cref="AtUri"/>s into the data needed to render an enhanced external embed.
    /// Returns `associatedRefs` (strongRefs to embed into a post's external.associatedRefs),
    /// the raw `associatedRecords`, and a hydrated `view`.
    /// The response is empty when no records were resolvable, or when validation determined the resolved records don't actually back the requested URL in the <paramref name="postView"/>;
    /// clients should fall back to their own link-card rendering in that case and skip writing strongRefs to the post.
    /// </summary>
    /// <param name="postView">The <see cref="PostView"/> containing the external embed to resolve.</param>
    /// <param name="subscribedLabelers">Optional list of subscribed labelers.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when a <paramref name="postView"/> or its Embed property is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">Thrown when the <paramref name="postView"/> does not contain a valid <see cref="EmbeddedExternalView"/> or its associated properties.</exception>
    public async Task<AtProtoHttpResult<EmbeddedExternalView>> GetEmbedExternalView(
        PostView postView,
        IEnumerable<Did>? subscribedLabelers)
    {
        return await GetEmbedExternalView(postView, subscribedLabelers, default).ConfigureAwait(false);
    }

    /// <summary>
    /// Resolve one or more <see cref="AtUri"/>s into the data needed to render an enhanced external embed.
    /// Returns `associatedRefs` (strongRefs to embed into a post's external.associatedRefs),
    /// the raw `associatedRecords`, and a hydrated `view`.
    /// The response is empty when no records were resolvable, or when validation determined the resolved records don't actually back the requested URL in the <paramref name="postView"/>;
    /// clients should fall back to their own link-card rendering in that case and skip writing strongRefs to the post.
    /// </summary>
    /// <param name="postView">The <see cref="PostView"/> containing the external embed to resolve.</param>
    /// <param name="subscribedLabelers">Optional list of subscribed labelers.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when a <paramref name="postView"/> or its Embed property is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">Thrown when the <paramref name="postView"/> does not contain a valid <see cref="EmbeddedExternalView"/> or its associated properties.</exception>
    public async Task<AtProtoHttpResult<EmbeddedExternalView>> GetEmbedExternalView(
        PostView postView,
        IEnumerable<Did>? subscribedLabelers,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(postView);
        ArgumentNullException.ThrowIfNull(postView.Embed);

        if (postView.Embed is not EmbeddedExternalView embeddedExternalView)
        {
            throw new ArgumentException($"The post view does not contain an {nameof(EmbeddedExternalView)}.", nameof(postView));
        }

        if (embeddedExternalView.External is null)
        {
            throw new ArgumentException($"The post view does not contain an {nameof(EmbeddedExternalView.External)}.", nameof(postView));
        }

        if (embeddedExternalView.External.AssociatedRefs is null || embeddedExternalView.External.AssociatedRefs.Count == 0)
        {
            throw new ArgumentException($"The post view does not contain any {nameof(EmbeddedExternalView.External.AssociatedRefs)}.", nameof(postView));
        }

        var atUris = embeddedExternalView.External.AssociatedRefs.Select(r => r.Uri).ToList();

        return await GetEmbedExternalView(embeddedExternalView.External.Uri, [.. atUris], subscribedLabelers, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns a new instance of a <see cref="OpenGraphEmbeddedCardGenerator"/> class, configured to use the agent.
    /// </summary>
    /// <returns>A new instance of <see cref="OpenGraphEmbeddedCardGenerator"/>.</returns>
    public OpenGraphEmbeddedCardGenerator CreateOpenGraphEmbeddedCardGenerator()
    {
        return new OpenGraphEmbeddedCardGenerator(
            agent: this,
            httpClient: HttpClient,
            loggerFactory: LoggerFactory);
    }

    /// <summary>
    /// Returns a new instance of a <see cref="StandardSiteEmbeddedCardGenerator"/> class, configured to use the agent.
    /// </summary>
    /// <returns>A new instance of <see cref="StandardSiteEmbeddedCardGenerator"/>.</returns>
    public StandardSiteEmbeddedCardGenerator CreateStandardSiteEmbeddedCardGenerator()
    {
        return new StandardSiteEmbeddedCardGenerator(
            agent: this,
            httpClient: HttpClient,
            loggerFactory: LoggerFactory);
    }
}