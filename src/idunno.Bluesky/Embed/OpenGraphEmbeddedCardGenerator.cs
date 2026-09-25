// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.RegularExpressions;

using idunno.AtProto;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace idunno.Bluesky.Embed;

/// <summary>
/// Creates an OpenCard embed, which is a card that can be embedded in Bluesky posts, displaying richer content from a URL.
/// This is used for embedding external content such as websites, videos, and other media.
/// </summary>
public partial class OpenGraphEmbeddedCardGenerator : BaseEmbeddedCardGenerator
{
    [GeneratedRegex("<meta\\s[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex s_MetaElementRegex();

    [GeneratedRegex("(?<name>[a-z0-9:_.-]+)\\s*=\\s*(?:\"(?<value>[^\"]*)\"|'(?<value>[^']*)'|(?<value>[^\\s\"'>]+))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex s_HtmlAttributeRegex();

    /// <summary>
    /// Creates a new instance of <see cref="OpenGraphEmbeddedCardGenerator"/>.
    /// </summary>
    /// <param name="agent">The <see cref="BlueskyAgent"/> to use for thumbnail uploading.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="agent"/> is <see langword="null" />.</exception>
    public OpenGraphEmbeddedCardGenerator(BlueskyAgent agent) : this(agent: agent, loggerFactory: null)
    {
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
        loggerFactory ??= NullLoggerFactory.Instance;
        ILogger = loggerFactory.CreateLogger<OpenGraphEmbeddedCardGenerator>();
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
        ArgumentNullException.ThrowIfNull(logger);
        ILogger = logger;
    }

    /// <summary>
    /// Creates a new instance of <see cref="OpenGraphEmbeddedCardGenerator"/>.
    /// </summary>
    /// <param name="agent">The <see cref="BlueskyAgent"/> to use for thumbnail uploading.</param>
    /// <param name="httpClient">The <see cref="HttpClient"/> to use for making HTTP requests to retrieve OpenGraph data. Ensure the client is hardened against SSRF attacks.</param>
    /// <param name="loggerFactory">The <see cref="ILoggerFactory"/> to use for logging. If <see langword="null" />, a no-op logger will be used.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="agent"/> is <see langword="null" />.</exception>
    public OpenGraphEmbeddedCardGenerator(BlueskyAgent agent, HttpClient httpClient, ILoggerFactory? loggerFactory)
        : base(agent, httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        loggerFactory ??= NullLoggerFactory.Instance;
        ILogger = loggerFactory.CreateLogger<OpenGraphEmbeddedCardGenerator>();
    }

    /// <summary>
    /// Creates a new instance of <see cref="OpenGraphEmbeddedCardGenerator"/>.
    /// </summary>
    /// <param name="agent">The <see cref="BlueskyAgent"/> to use for thumbnail uploading.</param>
    /// <param name="httpClient">The <see cref="HttpClient"/> to use for making HTTP requests to retrieve OpenGraph data. Ensure the client is hardened against SSRF attacks.</param>
    /// <param name="logger">The <see cref="ILogger"/> to use for logging.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="agent"/>, <paramref name="httpClient"/>, or <paramref name="logger"/> is <see langword="null" />.</exception>
    protected OpenGraphEmbeddedCardGenerator(BlueskyAgent agent, HttpClient httpClient, ILogger logger)
        : base(agent, httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(logger);

        ILogger = logger;
    }

    /// <summary>
    /// Gets an <see cref="EmbeddedExternal"/> for <paramref name="uri"/>, preferring OpenGraph data if available.
    /// </summary>
    /// <param name="uri">The URI to retrieve OpenGraph data from.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>An <see cref="EmbeddedExternal"/> if the page can be fetched and a valid canonical URL can be determined; otherwise, <see langword="null"/>.</returns>
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

        string? pageContent = await GetPageContent(uri, cancellationToken: cancellationToken).ConfigureAwait(false);

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
    /// <returns>An <see cref="EmbeddedExternal"/> if enough OpenGraph data is found; otherwise, <see langword="null"/>.</returns>
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

        Dictionary<string, string> openGraphProperties = new(StringComparer.OrdinalIgnoreCase);

        foreach (Match metaElement in s_MetaElementRegex().Matches(pageContent))
        {
            string? name = null;
            string? content = null;

            foreach (Match attribute in s_HtmlAttributeRegex().Matches(metaElement.Value))
            {
                string attributeName = attribute.Groups["name"].Value;

                // OpenGraph specifies property=, but name= is commonly used instead, and either may appear before or after content=.
                if (name is null &&
                    (attributeName.Equals("property", StringComparison.OrdinalIgnoreCase) || attributeName.Equals("name", StringComparison.OrdinalIgnoreCase)))
                {
                    name = attribute.Groups["value"].Value;
                }
                else if (content is null && attributeName.Equals("content", StringComparison.OrdinalIgnoreCase))
                {
                    content = attribute.Groups["value"].Value;
                }
            }

            if (name is null || content is null || !name.StartsWith("og:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string property = name["og:".Length..];

            if (!string.IsNullOrEmpty(property) && !openGraphProperties.ContainsKey(property))
            {
                // Attribute values are HTML encoded, so an entity in the markup would otherwise be posted verbatim in the card.
                openGraphProperties.Add(property, WebUtility.HtmlDecode(content));
            }
        }

        // Look for the basic OpenGraph properties that are required for an OpenCard embed.
        string? title = openGraphProperties.TryGetValue("title", out string? titleValue) ? titleValue : null;

        // We at least need a title.
        if (string.IsNullOrEmpty(title))
        {
            return null;
        }

        string? canonicalUrl = openGraphProperties.TryGetValue("url", out string? openGraphUrl) ? openGraphUrl : uri.ToString();
        if (!Uri.TryCreate(canonicalUrl, UriKind.Absolute, out Uri? canonicalUri))
        {
            return null;
        }

        // og:url is supplied by the page, and an absolute URI is not necessarily a web one. Without this a page can put
        // a javascript: or data: URI into the card the caller is about to post.
        if (!canonicalUri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !canonicalUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            Logger.EmbeddedCardCanonicalUrlSchemeNotSupported(ILogger, uri, canonicalUrl);
            return null;
        }

        string description = openGraphProperties.TryGetValue("description", out string? descriptionValue) ? descriptionValue : string.Empty;
        Blob? thumb = null;

        if (openGraphProperties.TryGetValue("image", out string? imageUrl) &&
            Agent.IsAuthenticated &&
            Uri.TryCreate(imageUrl, UriKind.Absolute, out Uri? imageUri))
        {
            thumb = await DownloadAndUploadImageBlob(
                imageUri,
                openGraphProperties.TryGetValue("image:type", out string? imageMimeType) ? imageMimeType : null,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        return new EmbeddedExternal(
            uri: canonicalUrl,
            title: title,
            description: description,
            thumbnail: thumb
        );
    }
}