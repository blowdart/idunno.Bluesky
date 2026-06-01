// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Logging;

using idunno.AtProto;
using idunno.AtProto.Repo;

namespace idunno.Bluesky.Embed;

/// <summary>
/// Generates an Embedded Card using Standard.Site and OpenGraph metadata from the target URI.
/// </summary>
public sealed partial class StandardSiteCardGenerator : OpenGraphEmbeddedCardGenerator
{
    [GeneratedRegex(@"<link rel=\""site.standard.([^\""]+)\"" href=\""([^\""]+)\""", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex s_SiteStandardLinkRegex();

    /// <summary>
    /// Creates a new instance of <see cref="StandardSiteCardGenerator"/>.
    /// </summary>
    /// <param name="agent">The <see cref="BlueskyAgent"/> to use for thumbnail uploading.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="agent"/> is <see langword="null" />.</exception>
    public StandardSiteCardGenerator(BlueskyAgent agent) : this(agent: agent, logger: null)
    {
        ArgumentNullException.ThrowIfNull(agent);
    }

    /// <summary>
    /// Creates a new instance of <see cref="StandardSiteCardGenerator"/>.
    /// </summary>
    /// <param name="agent">The <see cref="BlueskyAgent"/> to use for thumbnail uploading.</param>
    /// <param name="logger">The <see cref="ILogger"/> to use for logging. If <see langword="null" />, a no-op logger will be used.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="agent"/> is <see langword="null" />.</exception>
    public StandardSiteCardGenerator(BlueskyAgent agent, ILogger<StandardSiteCardGenerator>? logger)
        : base(agent, agent?.HttpClient!, logger!)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(agent.HttpClient);
    }

    /// <summary>
    /// Creates a new instance of <see cref="StandardSiteCardGenerator"/>.
    /// </summary>
    /// <param name="agent">The <see cref="BlueskyAgent"/> to use for thumbnail uploading.</param>
    /// <param name="httpClient">The <see cref="HttpClient"/> to use for making HTTP requests to retrieve OpenGraph data.</param>
    /// <param name="logger">The <see cref="Microsoft.Extensions.Logging.ILogger"/> to use for logging. If <see langword="null" />, a no-op logger will be used.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="agent"/> is <see langword="null" />.</exception>
    public StandardSiteCardGenerator(BlueskyAgent agent, HttpClient httpClient, ILogger<StandardSiteCardGenerator> logger)
        : base(agent, httpClient, logger)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(logger);
    }

    /// <summary>
    /// Gets an <see cref="EmbeddedExternal"/> for <paramref name="uri"/>, prefering OpenGraph data if available.
    /// </summary>
    /// <param name="uri">The URI to retrieve OpenGraph data from.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>An <see cref="EmbeddedExternal"/> if OpenGraph data is found; otherwise, <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="uri"/> is <see langword="null" /></exception>
    public override async Task<EmbeddedExternal?> Generate(Uri uri, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);

        string? pageContent = await GetPageContent(uri, cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(pageContent))
        {
            return await CreateEmbeddedExternalFromSiteStandardMetadata(uri, pageContent, cancellationToken).ConfigureAwait(false);
        }

        return null;
    }

    /// <summary>Creates an <see cref="EmbeddedExternal"/> from OpenGraph metadata, supplemented with Standard.Site metadata if available.</summary>
    /// <param name="uri">The URI to retrieve OpenGraph and Standard.Site data from.</param>
    /// <param name="pageContent">The HTML content of the page.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>An <see cref="EmbeddedExternal"/> if OpenGraph data is found; otherwise, <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="uri"/> is <see langword="null" /></exception>
    [SuppressMessage("Minor Code Smell", "S3267:Loops should be simplified with \"LINQ\" expressions", Justification = "Avoid linq allocations in a hot path.")]
    private async Task<EmbeddedExternal?> CreateEmbeddedExternalFromSiteStandardMetadata(Uri uri, string pageContent, CancellationToken cancellationToken = default)
    {
        EmbeddedExternal? result = await CreateEmbeddedExternalFromOpenGraphMetadata(uri, pageContent, cancellationToken).ConfigureAwait(false);

        Dictionary<string, string> siteStandardLinks = [];

        foreach (Match match in s_SiteStandardLinkRegex().Matches(pageContent))
        {
            string property = match.Groups[1].Value;
            string content = match.Groups[2].Value;

            if (!string.IsNullOrEmpty(property) && !siteStandardLinks.ContainsKey(property))
            {
                siteStandardLinks.Add(property, content);
            }
        }

        string? documentMetadata = siteStandardLinks.TryGetValue("document", out string? value) ? value : null;
        string? publicationMetadata = siteStandardLinks.TryGetValue("publication", out value) ? value : null;

        AtUri? documentAtUri = null;

        if (documentMetadata is not null && AtUri.TryParse(documentMetadata, out documentAtUri) && publicationMetadata is null)
        {
            UriBuilder publicationMetaDataPathBuilder = new()
            {
                Scheme = uri.Scheme,
                Host = uri.Host,
                Port = uri.Port,
                Path = "/.well-known/site.standard.publication"
            };

            publicationMetadata = await GetPageContent(publicationMetaDataPathBuilder.Uri, cancellationToken).ConfigureAwait(false);
        }

        if (documentAtUri is not null && publicationMetadata is not null && AtUri.TryParse(publicationMetadata, out AtUri? publicationAtUri))
        {
            if (documentAtUri.Repo is null)
            {
                Logger.FailedToFindOrParseSiteStandardDocumentLink(ILogger, uri);
                return result;
            }

            Did? documentAuthorDid = null;
            if (publicationAtUri.Repo is Handle documentAuthorHandle)
            {
                documentAuthorDid = await Agent.ResolveHandle(documentAuthorHandle, cancellationToken).ConfigureAwait(false);
            }
            else if (publicationAtUri.Repo is Did did)
            {
                documentAuthorDid = did;
            }

            if (documentAuthorDid is null)
            {
                Logger.AuthorDidRepoNotPresentInSiteStandardDocumentLink(ILogger, documentAtUri, uri);
                return result;
            }

            Uri? documentRecordPds = await Agent.ResolvePds(documentAuthorDid, cancellationToken).ConfigureAwait(false);
            if (documentRecordPds is null)
            {
                Logger.PdsForAuthorDidNotFound(ILogger, documentAuthorDid, uri);
                return result;
            }

            AtProtoHttpResult<AtProtoRepositoryRecord> documentRecordResult = await Agent.GetRawRecord(
                uri: documentAtUri,
                pds: documentRecordPds,
                cancellationToken:  cancellationToken).ConfigureAwait(false);

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
                    Logger.PublicationDidRepoNotPresent(ILogger, publicationAtUri, uri);
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
                    Logger.PdsForPublicationDidNotFound(ILogger, publicationAuthorDid, uri);
                    return result;
                }

                AtProtoHttpResult<AtProtoRepositoryRecord> publicationRecordResult = await Agent.GetRawRecord(
                    uri: publicationAtUri,
                    pds: publicationRecordPds,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                if (publicationRecordResult.Succeeded)
                {
                    if (result is null)
                    {
                        result = new EmbeddedExternal(
                            uri: uri,
                            title: uri.ToString(),
                            description: null,
                            thumbnail: null,
                            associatedRefs: [documentRecordResult.Result.StrongReference, publicationRecordResult.Result.StrongReference]);
                    }
                    else
                    {
                        result.External.AssociatedRefs = [documentRecordResult.Result.StrongReference, publicationRecordResult.Result.StrongReference];
                    }
                }
            }
        }

        return result;
    }
}
