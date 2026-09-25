// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.RegularExpressions;

using idunno.AtProto;
using idunno.AtProto.Repo;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace idunno.Bluesky.Embed;

/// <summary>
/// Generates an Embedded Card using Standard.Site and OpenGraph metadata from the target URI.
/// </summary>
public sealed partial class StandardSiteEmbeddedCardGenerator : OpenGraphEmbeddedCardGenerator
{
    [GeneratedRegex("<link\\s[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex s_LinkElementRegex();

    [GeneratedRegex("(?<name>[a-z0-9:_.-]+)\\s*=\\s*(?:\"(?<value>[^\"]*)\"|'(?<value>[^']*)'|(?<value>[^\\s\"'>]+))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex s_LinkAttributeRegex();

    /// <summary>
    /// Creates a new instance of <see cref="StandardSiteEmbeddedCardGenerator"/>.
    /// </summary>
    /// <param name="agent">The <see cref="BlueskyAgent"/> to use for thumbnail uploading.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="agent"/> is <see langword="null" />.</exception>
    public StandardSiteEmbeddedCardGenerator(BlueskyAgent agent) : this(agent: agent, loggerFactory: null)
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="StandardSiteEmbeddedCardGenerator"/>.
    /// </summary>
    /// <param name="agent">The <see cref="BlueskyAgent"/> to use for thumbnail uploading.</param>
    /// <param name="loggerFactory">The <see cref="ILoggerFactory"/> to use for logging. If <see langword="null" />, a no-op logger will be used.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="agent"/> is <see langword="null" />.</exception>
    public StandardSiteEmbeddedCardGenerator(BlueskyAgent agent, ILoggerFactory? loggerFactory)
        : base(agent: agent)
    {
        loggerFactory ??= NullLoggerFactory.Instance;
        ILogger = loggerFactory.CreateLogger<StandardSiteEmbeddedCardGenerator>();
    }

    /// <summary>
    /// Creates a new instance of <see cref="StandardSiteEmbeddedCardGenerator"/>.
    /// </summary>
    /// <param name="agent">The <see cref="BlueskyAgent"/> to use for thumbnail uploading.</param>
    /// <param name="httpClient">The <see cref="HttpClient"/> to use for making HTTP requests to retrieve Standard.Site data. Ensure the client is hardened against SSRF attacks.</param>
    /// <param name="loggerFactory">The <see cref="ILoggerFactory"/> to use for logging. If <see langword="null" />, a no-op logger will be used.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="agent"/> is <see langword="null" />.</exception>
    public StandardSiteEmbeddedCardGenerator(BlueskyAgent agent, HttpClient httpClient, ILoggerFactory? loggerFactory)
        : base(agent, httpClient, loggerFactory)
    {
        loggerFactory ??= NullLoggerFactory.Instance;
        ILogger = loggerFactory.CreateLogger<StandardSiteEmbeddedCardGenerator>();
    }

    /// <summary>
    /// Creates a new instance of <see cref="StandardSiteEmbeddedCardGenerator"/>.
    /// </summary>
    /// <param name="agent">The <see cref="BlueskyAgent"/> to use for thumbnail uploading.</param>
    /// <param name="httpClient">The <see cref="HttpClient"/> to use for making HTTP requests to retrieve Standard.Site data. Ensure the client is hardened against SSRF attacks.</param>
    /// <param name="logger">The <see cref="ILogger"/> to use for logging. If <see langword="null" />, a no-op logger will be used.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="agent"/> is <see langword="null" />.</exception>
    public StandardSiteEmbeddedCardGenerator(BlueskyAgent agent, HttpClient httpClient, ILogger logger)
        : base(agent, httpClient, logger)
    {
        ILogger = logger;
    }

    /// <summary>
    /// Gets an <see cref="EmbeddedExternal"/> for <paramref name="uri"/>, preferring Standard.Site metadata if available.
    /// </summary>
    /// <param name="uri">The URI to retrieve Standard.Site data from.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>An <see cref="EmbeddedExternal"/> if the page can be fetched and contains correct metadata. If only OpenGraph metadata is available an OpenGraph embed will be returned; otherwise, <see langword="null"/>.</returns>
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
            return await CreateEmbeddedExternalFromSiteStandardMetadata(uri, pageContent, cancellationToken).ConfigureAwait(false);
        }

        return null;
    }

    /// <summary>Creates an <see cref="EmbeddedExternal"/> from Standard.Site metadata, supplemented with OpenGraph metadata if available.</summary>
    /// <param name="uri">The URI to retrieve Standard.Site and OpenGraph data from.</param>
    /// <param name="pageContent">The HTML content of the page.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>An <see cref="EmbeddedExternal"/> if Standard.Site metadata is found; otherwise attempts to fall back to an OpenGraph embed, and if that fails, <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="uri"/> is <see langword="null" /></exception>
    [SuppressMessage("Minor Code Smell", "S3267:Loops should be simplified with \"LINQ\" expressions", Justification = "Avoid linq allocations in a hot path.")]
    private async Task<EmbeddedExternal?> CreateEmbeddedExternalFromSiteStandardMetadata(Uri uri, string pageContent, CancellationToken cancellationToken = default)
    {
        EmbeddedExternal? result = await CreateEmbeddedExternalFromOpenGraphMetadata(uri, pageContent, cancellationToken).ConfigureAwait(false);

        // We don't have any open graph metadata so we can't supplement it with the standard site metadata, so we might as well just return null at this point.
        if (result == null)
        {
            return result;
        }

        Dictionary<string, string> siteStandardLinks = new(StringComparer.OrdinalIgnoreCase);

        foreach (Match linkElement in s_LinkElementRegex().Matches(pageContent))
        {
            string? rel = null;
            string? href = null;

            foreach (Match attribute in s_LinkAttributeRegex().Matches(linkElement.Value))
            {
                string attributeName = attribute.Groups["name"].Value;

                // rel and href may appear in either order, in any casing, and with either quoting style.
                if (rel is null && attributeName.Equals("rel", StringComparison.OrdinalIgnoreCase))
                {
                    rel = attribute.Groups["value"].Value;
                }
                else if (href is null && attributeName.Equals("href", StringComparison.OrdinalIgnoreCase))
                {
                    href = attribute.Groups["value"].Value;
                }
            }

            if (rel is null || href is null || !rel.StartsWith("site.standard.", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string property = rel["site.standard.".Length..];

            if (!string.IsNullOrEmpty(property) && !siteStandardLinks.ContainsKey(property))
            {
                siteStandardLinks.Add(property, WebUtility.HtmlDecode(href));
            }
        }

        string? documentMetadata = siteStandardLinks.TryGetValue("document", out string? value) ? value : null;
        string? publicationMetadata = siteStandardLinks.TryGetValue("publication", out value) ? value : null;

        // A minimum of the document metadata is required to have a valid standard site embed,
        // so if it's not present, return the open graph result (which may be null if there was no open graph metadata).
        if (documentMetadata is null)
        {
            return result;
        }

        // If the document metadata is not an AtUri we can't do anything with it, so return the open graph result (which may be null if there was no open graph metadata).
        if (!AtUri.TryParse(documentMetadata.Trim(), out AtUri? documentAtUri))
        {
            return result;
        }

        if (publicationMetadata is null)
        {
            // Try to resolve via /.well-known/
            UriBuilder publicationMetaDataPathBuilder = new()
            {
                Scheme = uri.Scheme,
                Host = uri.Host,
                Port = uri.Port,
                Path = "/.well-known/site.standard.publication"
            };

            // A publication record is one short AT URI, so it is read with the well known budget rather than the budget
            // for a whole web page.
            publicationMetadata = await GetPlainTextContent(
                publicationMetaDataPathBuilder.Uri,
                maxDocumentSize: AtProtoServer.DefaultMaximumWellKnownResponseSize,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        // If there is no publication metadata after trying embeds and the well-known path
        // we can't create a standard site embed, so return the open graph result (which may be null if there was no open graph metadata).
        if (publicationMetadata is null)
        {
            return result;
        }

        // If the publication metadata is not an AtUri we can't do anything with it, so return the open graph result (which may be null if there was no open graph metadata).
        if (!AtUri.TryParse(publicationMetadata.Trim(), out AtUri? publicationAtUri))
        {
            return result;
        }

        Did? documentAuthorDid = null;
        if (documentAtUri.Repo is Handle documentAuthorHandle)
        {
            documentAuthorDid = await Agent.ResolveHandle(documentAuthorHandle, cancellationToken).ConfigureAwait(false);
        }
        else if (documentAtUri.Repo is Did did)
        {
            documentAuthorDid = did;
        }

        if (documentAuthorDid is null)
        {
            Bluesky.Logger.AuthorDidResolutionFailed(ILogger, uri, documentAtUri);
            return result;
        }

        Uri? documentRecordPds = await Agent.ResolvePds(documentAuthorDid, cancellationToken).ConfigureAwait(false);
        if (documentRecordPds is null)
        {
            Bluesky.Logger.PdsForAuthorDidNotFound(ILogger, documentAuthorDid, uri);
            return result;
        }

        AtProtoHttpResult<AtProtoRepositoryRecord> documentRecordResult = await Agent.GetRawRecord(
            uri: documentAtUri,
            pds: documentRecordPds,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (documentRecordResult.Succeeded)
        {
            Did? publicationAuthorDid = null;

            if (documentAtUri.Repo.Equals(publicationAtUri.Repo))
            {
                publicationAuthorDid = documentAuthorDid;
            }
            else if (publicationAtUri.Repo is Handle publicationAuthorHandle)
            {
                publicationAuthorDid = await Agent.ResolveHandle(publicationAuthorHandle, cancellationToken).ConfigureAwait(false);
            }
            else if (publicationAtUri.Repo is Did did)
            {
                publicationAuthorDid = did;
            }

            if (publicationAuthorDid is null)
            {
                Bluesky.Logger.PublicationDidRepoNotPresent(ILogger, publicationAtUri, uri);
                return result;
            }

            Uri? publicationRecordPds;

            if (documentAuthorDid.Equals(publicationAuthorDid))
            {
                publicationRecordPds = documentRecordPds;
            }
            else
            {
                publicationRecordPds = await Agent.ResolvePds(publicationAuthorDid, cancellationToken).ConfigureAwait(false);
            }

            if (publicationRecordPds is null)
            {
                Bluesky.Logger.PdsForPublicationDidNotFound(ILogger, publicationAuthorDid, uri);
                return result;
            }

            AtProtoHttpResult<AtProtoRepositoryRecord> publicationRecordResult = await Agent.GetRawRecord(
                uri: publicationAtUri,
                pds: publicationRecordPds,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (publicationRecordResult.Succeeded)
            {
                result.External.AssociatedRefs = [documentRecordResult.Result.StrongReference, publicationRecordResult.Result.StrongReference];
            }
        }

        return result;
    }
}