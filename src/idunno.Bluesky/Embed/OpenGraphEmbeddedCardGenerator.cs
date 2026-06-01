// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using idunno.AtProto;

namespace idunno.Bluesky.Embed;

/// <summary>
/// Creates an OpenCard embed, which is a card that can be embedded in Bluesky posts, displaying richer content from a URL.
/// This is used for embedding external content such as websites, videos, and other media.
/// </summary>
public partial class OpenGraphEmbeddedCardGenerator : BaseEmbeddedCardGenerator
{
    [GeneratedRegex(@"<meta property=\""og:([^\""]+)\"" content=\""([^\""]+)\""", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex s_OpenGraphPropertyRegex();

    [GeneratedRegex(@"<title>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex s_TitleTag();

    /// <summary>
    /// Creates a new instance of <see cref="OpenGraphEmbeddedCardGenerator"/>.
    /// </summary>
    /// <param name="agent">The <see cref="BlueskyAgent"/> to use for thumbnail uploading.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="agent"/> is <see langword="null" />.</exception>
    public OpenGraphEmbeddedCardGenerator(BlueskyAgent agent) : this(agent: agent, loggerFactory: null)
    {
        ArgumentNullException.ThrowIfNull(agent);
    }

    /// <summary>
    /// Creates a new instance of <see cref="OpenGraphEmbeddedCardGenerator"/>.
    /// </summary>
    /// <param name="agent">The <see cref="BlueskyAgent"/> to use for thumbnail uploading.</param>
    /// <param name="loggerFactory">The <see cref="ILoggerFactory"/> to use for logging. If <see langword="null" />, a no-op logger will be used.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="agent"/> is <see langword="null" />.</exception>
    public OpenGraphEmbeddedCardGenerator(BlueskyAgent agent, ILoggerFactory? loggerFactory)
        : base(agent, agent?.HttpClient)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(agent.HttpClient);

        loggerFactory ??= NullLoggerFactory.Instance;
        Logger = loggerFactory.CreateLogger<OpenGraphEmbeddedCardGenerator>();
    }

    /// <summary>
    /// Creates a new instance of <see cref="OpenGraphEmbeddedCardGenerator"/>.
    /// </summary>
    /// <param name="agent">The <see cref="BlueskyAgent"/> to use for thumbnail uploading.</param>
    /// <param name="logger">The <see cref="ILogger"/> to use for logging.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="agent"/> or <paramref name="logger"/> is <see langword="null" />.</exception>
    protected OpenGraphEmbeddedCardGenerator(BlueskyAgent agent, ILogger logger)
        : base(agent, agent?.HttpClient)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(agent.HttpClient);
        ArgumentNullException.ThrowIfNull(logger);
        Logger = logger;
    }

    /// <summary>
    /// Creates a new instance of <see cref="OpenGraphEmbeddedCardGenerator"/>.
    /// </summary>
    /// <param name="agent">The <see cref="BlueskyAgent"/> to use for thumbnail uploading.</param>
    /// <param name="httpClient">The <see cref="HttpClient"/> to use for making HTTP requests to retrieve OpenGraph data.</param>
    /// <param name="loggerFactory">The <see cref="ILoggerFactory"/> to use for logging. If <see langword="null" />, a no-op logger will be used.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="agent"/> is <see langword="null" />.</exception>
    public OpenGraphEmbeddedCardGenerator(BlueskyAgent agent, HttpClient httpClient, ILoggerFactory? loggerFactory)
        : base(agent, httpClient)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(httpClient);

        loggerFactory ??= NullLoggerFactory.Instance;
        Logger = loggerFactory.CreateLogger<OpenGraphEmbeddedCardGenerator>();
    }

    /// <summary>
    /// Creates a new instance of <see cref="OpenGraphEmbeddedCardGenerator"/>.
    /// </summary>
    /// <param name="agent">The <see cref="BlueskyAgent"/> to use for thumbnail uploading.</param>
    /// <param name="httpClient">The <see cref="HttpClient"/> to use for making HTTP requests to retrieve OpenGraph data.</param>
    /// <param name="logger">The <see cref="ILogger"/> to use for logging.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="agent"/>, <paramref name="httpClient"/>, or <paramref name="logger"/> is <see langword="null" />.</exception>
    protected OpenGraphEmbeddedCardGenerator(BlueskyAgent agent, HttpClient httpClient, ILogger logger)
        : base(agent, httpClient)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(logger);

        Logger = logger;
    }

    /// <summary>
    /// Gets an <see cref="EmbeddedExternal"/> for <paramref name="uri"/>, preferring OpenGraph data if available.
    /// </summary>
    /// <param name="uri">The URI to retrieve OpenGraph data from.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>An <see cref="EmbeddedExternal"/> if OpenGraph data is found; otherwise, <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="uri"/> is <see langword="null" /></exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="uri"/> is not an absolute URI.</exception>
    [SuppressMessage("Documentation", "CSENSE020:Potential ghost parameter reference in documentation", Justification = "Not a ghost reference.")]
    public override async Task<EmbeddedExternal?> Generate(Uri uri, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (!uri.IsAbsoluteUri)
        {
            throw new ArgumentException("URI must be absolute.", nameof(uri));
        }

        string? pageContent = await GetPageContent(uri, cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(pageContent))
        {
            return await CreateEmbeddedExternalFromOpenGraphMetadata(uri, pageContent, cancellationToken).ConfigureAwait(false);
        }

        return null;
    }

    /// <summary>Creates an <see cref="EmbeddedExternal"/> from OpenGraph metadata.</summary>
    /// <param name="uri">The URI to retrieve OpenGraph data from.</param>
    /// <param name="pageContent">The HTML content of the page.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>An <see cref="EmbeddedExternal"/> if OpenGraph data is found; otherwise, <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="uri"/> is <see langword="null" /></exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="uri"/> is not an absolute URI.</exception>
    [SuppressMessage("Minor Code Smell", "S3267:Loops should be simplified with \"LINQ\" expressions", Justification = "Avoid linq allocations in a hot path.")]
    [SuppressMessage("Documentation", "CSENSE020:Potential ghost parameter reference in documentation", Justification = "Not a ghost reference.")]
    protected async Task<EmbeddedExternal?> CreateEmbeddedExternalFromOpenGraphMetadata(Uri uri, string pageContent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (!uri.IsAbsoluteUri)
        {
            throw new ArgumentException("URI must be absolute.", nameof(uri));
        }

        Dictionary<string, string> openGraphProperties = [];

        foreach (Match match in s_OpenGraphPropertyRegex().Matches(pageContent))
        {
            string property = match.Groups[1].Value;
            string content = match.Groups[2].Value;

            if (!string.IsNullOrEmpty(property) && !openGraphProperties.ContainsKey(property))
            {
                openGraphProperties.Add(property, content);
            }
        }

        // Look for the basic OpenGraph properties that are required for an OpenCard embed. If they are not present, fall back to using the page title and supplied URI.
        string? canonicalUrl = openGraphProperties.TryGetValue("url", out string? openGraphUrl) ? openGraphUrl : uri.ToString();
        if (!Uri.TryCreate(canonicalUrl, UriKind.Absolute, out Uri? _))
        {
            return null;
        }

        string? title = openGraphProperties.TryGetValue("title", out string? titleValue) ? titleValue : canonicalUrl;
        string? description = openGraphProperties.TryGetValue("description", out string? descriptionValue) ? descriptionValue : null;
        Blob? thumb = null;

        // No OpenGraph title property found, so fall back to using the page title or URL as the embed title.
        if (string.IsNullOrEmpty(title))
        {
            Match titleMatch = s_TitleTag().Match(pageContent);
            if (titleMatch.Success)
            {
                title = titleMatch.Groups[1].Value;
            }
            else
            {
                title = uri.ToString();
            }
        }

        if (openGraphProperties.TryGetValue("image", out string? imageUrl) &&
            Agent is not null &&
            Agent.IsAuthenticated &&
            Uri.TryCreate(imageUrl, UriKind.Absolute, out Uri? imageUri))
        {
            thumb = await DownloadAndUploadImageBlob(
                imageUri,
                openGraphProperties.TryGetValue("image:type", out string? imageMimeType) ? imageMimeType : null,
                cancellationToken).ConfigureAwait(false);
        }

        return new EmbeddedExternal(
            uri: canonicalUrl,
            title: title ?? canonicalUrl,
            description: description,
            thumbnail: thumb
        );
    }
}
