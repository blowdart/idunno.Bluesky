// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;

using idunno.AtProto;
using idunno.Bluesky.Embed;
using idunno.Bluesky.Feed;

namespace idunno.Bluesky;

partial class BlueskyAgent
{
    /// <summary>
    /// Resolve one or more <see cref="AtUri"/>s into the data needed to render an enhanced external embed.
    /// </summary>
    /// <param name="url">The canonical web URL the embed represents (typically the URL the user pasted into the composer). Used as the returned view's `uri`. May be used for validation in the future.</param>
    /// <param name="uris">An array of AT-URIs to resolve into the data needed for the embed.</param>
    /// <param name="subscribedLabelers">Optional list of subscribed labelers.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when a required argument is <see langword="null" />.</exception>
    /// <remarks>
    /// <para>Only the hydrated view is returned. The raw associated records the service returns are not surfaced.</para>
    /// <para>The service returns an empty response when no records were resolvable, or when validation determined the resolved records do not actually
    /// back the requested <paramref name="url" />. That case is reported as an unsuccessful result whose <see cref="AtProtoHttpResult{T}.StatusCode"/> is
    /// <see cref="HttpStatusCode.NoContent"/> and whose <see cref="AtProtoHttpResult{T}.AtErrorDetail"/> is <see langword="null" />. Callers should fall
    /// back to their own link card rendering in that case and skip writing strong references to the post.</para>
    /// </remarks>
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
            maximumResponseSize: MaximumResponseSize,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Resolve one or more <see cref="AtUri"/>s into the data needed to render an enhanced external embed.
    /// </summary>
    /// <param name="postView">The <see cref="PostView"/> containing the external embed to resolve.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when a <paramref name="postView"/> or its Embed property is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">Thrown when the <paramref name="postView"/> does not contain an <see cref="EmbeddedExternalView"/>.</exception>
    /// <remarks>
    /// <para>Only the hydrated view is returned. The raw associated records the service returns are not surfaced.</para>
    /// <para>The service returns an empty response when no records were resolvable, or when validation determined the resolved records do not actually
    /// back the requested link. That case is reported as an unsuccessful result whose <see cref="AtProtoHttpResult{T}.StatusCode"/> is
    /// <see cref="HttpStatusCode.NoContent"/> and whose <see cref="AtProtoHttpResult{T}.AtErrorDetail"/> is <see langword="null" />. Callers should fall
    /// back to their own link card rendering in that case and skip writing strong references to the post.</para>
    /// </remarks>
    public async Task<AtProtoHttpResult<EmbeddedExternalView>> GetEmbedExternalView(
        PostView postView)
    {
        return await GetEmbedExternalView(postView, null, default).ConfigureAwait(false);
    }

    /// <summary>
    /// Resolve one or more <see cref="AtUri"/>s into the data needed to render an enhanced external embed.
    /// </summary>
    /// <param name="postView">The <see cref="PostView"/> containing the external embed to resolve.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when a <paramref name="postView"/> or its Embed property is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">Thrown when the <paramref name="postView"/> does not contain an <see cref="EmbeddedExternalView"/>.</exception>
    /// <remarks>
    /// <para>Only the hydrated view is returned. The raw associated records the service returns are not surfaced.</para>
    /// <para>The service returns an empty response when no records were resolvable, or when validation determined the resolved records do not actually
    /// back the requested link. That case is reported as an unsuccessful result whose <see cref="AtProtoHttpResult{T}.StatusCode"/> is
    /// <see cref="HttpStatusCode.NoContent"/> and whose <see cref="AtProtoHttpResult{T}.AtErrorDetail"/> is <see langword="null" />. Callers should fall
    /// back to their own link card rendering in that case and skip writing strong references to the post.</para>
    /// </remarks>
    public async Task<AtProtoHttpResult<EmbeddedExternalView>> GetEmbedExternalView(
        PostView postView,
        CancellationToken cancellationToken)
    {
        return await GetEmbedExternalView(postView, null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Resolve one or more <see cref="AtUri"/>s into the data needed to render an enhanced external embed.
    /// </summary>
    /// <param name="postView">The <see cref="PostView"/> containing the external embed to resolve.</param>
    /// <param name="subscribedLabelers">Optional list of subscribed labelers.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when a <paramref name="postView"/> or its Embed property is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">Thrown when the <paramref name="postView"/> does not contain an <see cref="EmbeddedExternalView"/>.</exception>
    /// <remarks>
    /// <para>Only the hydrated view is returned. The raw associated records the service returns are not surfaced.</para>
    /// <para>The service returns an empty response when no records were resolvable, or when validation determined the resolved records do not actually
    /// back the requested link. That case is reported as an unsuccessful result whose <see cref="AtProtoHttpResult{T}.StatusCode"/> is
    /// <see cref="HttpStatusCode.NoContent"/> and whose <see cref="AtProtoHttpResult{T}.AtErrorDetail"/> is <see langword="null" />. Callers should fall
    /// back to their own link card rendering in that case and skip writing strong references to the post.</para>
    /// </remarks>
    public async Task<AtProtoHttpResult<EmbeddedExternalView>> GetEmbedExternalView(
        PostView postView,
        IEnumerable<Did>? subscribedLabelers)
    {
        return await GetEmbedExternalView(postView, subscribedLabelers, default).ConfigureAwait(false);
    }

    /// <summary>
    /// Resolve one or more <see cref="AtUri"/>s into the data needed to render an enhanced external embed.
    /// </summary>
    /// <param name="postView">The <see cref="PostView"/> containing the external embed to resolve.</param>
    /// <param name="subscribedLabelers">Optional list of subscribed labelers.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when a <paramref name="postView"/> or its Embed property is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">Thrown when the <paramref name="postView"/> does not contain an <see cref="EmbeddedExternalView"/>.</exception>
    /// <remarks>
    /// <para>Only the hydrated view is returned. The raw associated records the service returns are not surfaced.</para>
    /// <para>The service returns an empty response when no records were resolvable, or when validation determined the resolved records do not actually
    /// back the requested link. That case is reported as an unsuccessful result whose <see cref="AtProtoHttpResult{T}.StatusCode"/> is
    /// <see cref="HttpStatusCode.NoContent"/> and whose <see cref="AtProtoHttpResult{T}.AtErrorDetail"/> is <see langword="null" />. Callers should fall
    /// back to their own link card rendering in that case and skip writing strong references to the post.</para>
    /// </remarks>
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

        if (embeddedExternalView.External.AssociatedRefs is null ||
            embeddedExternalView.External.AssociatedRefs.Count == 0 ||
            !Uri.TryCreate(embeddedExternalView.External.Uri, UriKind.Absolute, out Uri? url))
        {
            // A post with no associated references, or whose external uri is not a legal absolute uri, cannot have an enhanced embed resolved for it.
            // That is ordinary data rather than a programming error, so it is reported the same way as an empty response from the service.
            return new AtProtoHttpResult<EmbeddedExternalView>(
                null,
                statusCode: HttpStatusCode.NoContent,
                httpResponseHeaders: null,
                atErrorDetail: null,
                rateLimit: null);
        }

        AtUri[] atUris = [.. embeddedExternalView.External.AssociatedRefs.Select(r => r.Uri).Take(Maximum.EmbedExternalViewUris)];

        return await GetEmbedExternalView(url, atUris, subscribedLabelers, cancellationToken).ConfigureAwait(false);
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