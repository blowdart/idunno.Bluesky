// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using idunno.AtProto;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using static System.Net.Mime.MediaTypeNames;

namespace idunno.Bluesky.Embed;

/// <summary>
/// Creates an OpenCard embed, which is a card that can be embedded in Bluesky posts, displaying richer content from a URL.
/// This is used for embedding external content such as websites, videos, and other media.
/// </summary>
public partial class OpenGraphClient : IDisposable
{
    private const string UnknownImageType = "application/octet-stream";

    private readonly BlueskyAgent? _agent;

    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenGraphClient> _logger;

    private bool _isDisposed;

    [GeneratedRegex(@"<meta property=\""og:([^\""]+)\"" content=\""([^\""]+)\""", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex s_OpenGraphPropertyRegex();

    [GeneratedRegex(@"<title>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex s_TitleTag();

    /// <summary>
    /// Creates a new instance of <see cref="OpenGraphClient"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This constructor is used when you want to use the OpenGraphClient without an authenticated agent, which means
    /// thumbnail images will not be in the embedded card returned from <see cref="GetOpenGraphEmbed(Uri, CancellationToken)"/>.
    /// </para>
    /// </remarks>
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Handler will be disposed by HttpClient")]
    public OpenGraphClient()
    {
        _logger = NullLoggerFactory.Instance.CreateLogger<OpenGraphClient>();
        _httpClient = new HttpClient(Security.SsrfSocketsHttpHandlerFactory.Create(), disposeHandler: true);
    }

    /// <summary>
    /// Creates a new instance of <see cref="OpenGraphClient"/>.
    /// </summary>
    /// <param name="agent">The <see cref="BlueskyAgent"/> to use for thumbnail uploading.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="agent"/> is <see langword="null" />.</exception>
    public OpenGraphClient(BlueskyAgent agent) : this(agent: agent, logger: null)
    {
        ArgumentNullException.ThrowIfNull(agent);
    }

    /// <summary>
    /// Creates a new instance of <see cref="OpenGraphClient"/>.
    /// </summary>
    /// <param name="agent">The <see cref="BlueskyAgent"/> to use for thumbnail uploading.</param>
    /// <param name="logger">The <see cref="Microsoft.Extensions.Logging.ILogger"/> to use for logging. If <see langword="null" />, a no-op logger will be used.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="agent"/> is <see langword="null" />.</exception>
    public OpenGraphClient(BlueskyAgent agent, ILogger<OpenGraphClient>? logger)
    {
        ArgumentNullException.ThrowIfNull(agent);

        _agent = agent;
        _httpClient = agent.HttpClient;
        _logger = logger ?? NullLoggerFactory.Instance.CreateLogger<OpenGraphClient>();
    }

    /// <summary>
    /// Creates a new instance of <see cref="OpenGraphClient"/>.
    /// </summary>
    /// <param name="agent">The <see cref="BlueskyAgent"/> to use for thumbnail uploading.</param>
    /// <param name="httpClient">The <see cref="HttpClient"/> to use for making HTTP requests to retrieve OpenGraph data.</param>
    /// <param name="logger">The <see cref="Microsoft.Extensions.Logging.ILogger"/> to use for logging. If <see langword="null" />, a no-op logger will be used.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="agent"/> is <see langword="null" />.</exception>
    public OpenGraphClient(BlueskyAgent agent, HttpClient httpClient, ILogger<OpenGraphClient> logger)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(logger);

        _agent = agent;
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Gets an <see cref="EmbeddedExternal"/> for <paramref name="uri"/>, prefering OpenGraph data if available.
    /// </summary>
    /// <param name="uri">The URI to retrieve OpenGraph data from.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>An <see cref="EmbeddedExternal"/> if OpenGraph data is found; otherwise, <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="uri"/> is <see langword="null" /></exception>
    public async Task<EmbeddedExternal?> GetOpenGraphEmbed(Uri uri, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);

        using (HttpRequestMessage httpRequest = new(HttpMethod.Get, uri))
        {
            httpRequest.Headers.Accept.Clear();
            httpRequest.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/html"));
            httpRequest.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/xhtml+xml"));
            httpRequest.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/xml", 0.9));
            httpRequest.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("*/*", 0.8));
            
            try
            {
                using HttpResponseMessage response = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    Logger.GetOpenGraphRequestFailedWithStatusCode(_logger, uri, response.StatusCode);
                    return null;
                }

                string pageContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                return await CreateEmbeddedExternal(uri, pageContent, cancellationToken).ConfigureAwait(false);

            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                Logger.GetOpenGraphDataThrew(_logger, uri, ex);
                return null;
            }
        }
    }

    [SuppressMessage("Minor Code Smell", "S3267:Loops should be simplified with \"LINQ\" expressions", Justification = "Avoid linq allocations in a hot path.")]
    private async Task<EmbeddedExternal?> CreateEmbeddedExternal(Uri uri, string pageContent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);

        Dictionary<string, string> openGraphProperties = [];

        foreach (Match match in s_OpenGraphPropertyRegex().Matches(pageContent))
        {
            string property = match.Groups[1].Value;
            string content = match.Groups[2].Value;

            if (!string.IsNullOrEmpty(property))
            {
                openGraphProperties.Add(property, content);
            }
        }

        // Look for the basic OpenGraph properties that are required for an OpenCard embed. If they are not present, fall back to using the page title and supplied URI.
        string? canonicalUrl = openGraphProperties.TryGetValue("url", out string? openGraphUrl) ? openGraphUrl : uri.ToString();
        string? title = openGraphProperties.TryGetValue("title", out string? titleValue) ? titleValue : null;
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

        if (openGraphProperties.TryGetValue("image", out string? imageUrl) && _agent is not null)
        {
            thumb = await DownloadAndUploadImageBlob(imageUrl, cancellationToken).ConfigureAwait(false);
        }

        return new EmbeddedExternal(
            uri: canonicalUrl,
            title: title,
            description: description,
            thumbnail: thumb
        );
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Error handling")]
    private async Task<Blob?> DownloadAndUploadImageBlob(string imageUrl, CancellationToken cancellationToken = default)
    {
        Blob? result = null;

        if (Uri.TryCreate(imageUrl, UriKind.Absolute, out Uri? imageUri))
        {
            try
            {
                using (HttpRequestMessage httpRequest = new(HttpMethod.Get, imageUri))
                {
                    httpRequest.Headers.Accept.Clear();
                    httpRequest.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("image/jpg"));
                    httpRequest.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("image/png"));
                    httpRequest.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("image/gif"));

                    using (HttpResponseMessage response = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false))
                    {
                        if (response.IsSuccessStatusCode)
                        {
                            byte[] imageData = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
                            string imageMimeType = SniffImageContentType(imageData) ?? UnknownImageType;

                            if (!imageMimeType.Equals(UnknownImageType, StringComparison.OrdinalIgnoreCase) && _agent is not null)
                            {
                                // Upload the image blob
                                AtProtoHttpResult<Blob> uploadResult = await _agent.UploadBlob(blob: imageData, mimeType: imageMimeType, cancellationToken: cancellationToken).ConfigureAwait(false);

                                if (uploadResult.Succeeded)
                                {
                                    result = uploadResult.Result;
                                }
                                else
                                {
                                    Logger.OpenGraphImageUploadFailed(_logger, imageUri, uploadResult.StatusCode, uploadResult.AtErrorDetail?.Error, uploadResult.AtErrorDetail?.Message);
                                }
                            }
                        }
                        else
                        {
                            Logger.GetOpenGraphDataFailed(_logger, imageUri, response.StatusCode);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.OpenGraphImageUploadThrew(_logger, imageUri, ex);
                return null;
            }
        }
        else
        {
            throw new ArgumentException("Invalid image URL", nameof(imageUrl));
        }

        return result;
    
    }

    private static string SniffImageContentType(byte[] imageData)
    {
        // Simple content type sniffing based on file signatures (magic numbers)
        if (imageData.Length >= 4)
        {
            if (imageData[0] == 0xFF && imageData[1] == 0xD8 && imageData[2] == 0xFF)
            {
                return "image/jpeg";
            }
            else if (imageData[0] == 0x89 && imageData[1] == 0x50 && imageData[2] == 0x4E && imageData[3] == 0x47)
            {
                return "image/png";
            }
            else if (imageData.Length >= 6 &&
                     imageData[0] == 0x47 && imageData[1] == 0x49 && imageData[2] == 0x46 &&
                     imageData[3] == 0x38 && (imageData[4] == 0x39 || imageData[4] == 0x37) &&
                     imageData[5] == 0x61)
            {
                return "image/gif";
            }
        }
        // Default to octet-stream if content type cannot be determined
        return UnknownImageType;
    }

    /// <summary>
    /// Releases the unmanaged resources used by the <see cref="Agent"/> and optionally disposes of the managed resources.
    /// </summary>
    /// <param name="disposing"><see langword="true"/> to release both managed and unmanaged resources; <see langword="false"/> to releases only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_isDisposed)
        {
            if (disposing)
            {
                _httpClient?.Dispose();
            }

            _isDisposed = true;
        }
    }

    /// <summary>
    /// Disposes of any managed and unmanaged resources used by the <see cref="Agent"/>.
    /// </summary>
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
