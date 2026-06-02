// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using idunno.AtProto;
using System.Buffers;
using System.Runtime.ConstrainedExecution;

namespace idunno.Bluesky.Embed;

/// <summary>
/// Abstract class for an <see cref="IEmbeddedCardGenerator"/> which generates an <see cref="EmbeddedExternal"/> card for a given URI from its metadata.
/// </summary>
public abstract class BaseEmbeddedCardGenerator : IEmbeddedCardGenerator, IDisposable
{
    private bool _isDisposed;

    /// <summary>
    /// Creates a new instance of <see cref="BaseEmbeddedCardGenerator"/>.
    /// </summary>
    /// <param name="agent">The <see cref="BlueskyAgent"/> used to upload images for embedded cards.</param>
    /// <param name="httpClient">The <see cref="HttpClient"/> used for making HTTP requests.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="agent"/> is <see langword="null"/>.</exception>
    protected BaseEmbeddedCardGenerator(BlueskyAgent agent, HttpClient? httpClient)
    {
        ArgumentNullException.ThrowIfNull(agent);

        Agent = agent;

        if (httpClient is null)
        {
            HttpClient = Agent.HttpClient;
        }
        else
        {
            HttpClient = httpClient;
        }
    }

    /// <summary>
    /// Gets the <see cref="BlueskyAgent"/> used to upload images for embedded cards.
    /// </summary>
    protected BlueskyAgent Agent { get; }

    /// <summary>
    /// Gets the <see cref="HttpClient"/> used for making HTTP requests.
    /// </summary>
    protected HttpClient HttpClient { get; }

    /// <summary>
    /// Gets the logger used to log messages
    /// </summary>
    protected ILogger ILogger { get; set; } = NullLogger.Instance;

    /// <summary>
    /// Gets the mime type returned if the content type of an image is unknown.
    /// </summary>
    protected static string UnknownImageType => "application/octet-stream";

    /// <summary>
    /// Generates an <see cref="EmbeddedExternal"/> record for <paramref name="uri"/>.
    /// </summary>
    /// <param name="uri">The URI to generate the card from.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>An <see cref="EmbeddedExternal"/> if OpenGraph data is found; otherwise, <see langword="null"/>.</returns>
    public abstract Task<EmbeddedExternal?> Generate(Uri uri, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the contents of the webpage at <paramref name="uri"/> as a string.
    /// </summary>
    /// <param name="uri">The URI of the webpage to retrieve.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The contents of the webpage as a string or <see langword="null"/> if the request fails.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="uri"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="uri"/> is not a valid http or https URI.</exception>
    [SuppressMessage("Documentation", "CSENSE020:Potential ghost parameter reference in documentation", Justification = "Not a ghost reference")]
    protected virtual async Task<string?> GetPageContent(Uri uri, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (!uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("URI scheme must be http or https.", nameof(uri));
        }

        using (HttpRequestMessage httpRequest = new(HttpMethod.Get, uri))
        {
            httpRequest.Headers.Accept.Clear();
            httpRequest.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/html"));
            httpRequest.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/xhtml+xml"));
            httpRequest.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/xml", 0.9));
            httpRequest.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("*/*", 0.8));

            try
            {
                using HttpResponseMessage response = await HttpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    Logger.EmbeddedCardGetRequestFailedWithStatusCode(ILogger, uri, response.StatusCode);
                    return null;
                }

                return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                Logger.EmbeddedCardGetRequestThrew(ILogger, uri, ex);
                return null;
            }
        }
    }

    /// <summary>
    /// Gets an image from the specified <paramref name="uri"/> and uploads it to blob storage via the <see cref="BlueskyAgent"/>.
    /// </summary>
    /// <param name="uri">The URI of the image to download.</param>
    /// <param name="imageMimeType">The mime type of the image, if known.</param>
    /// <param name="maxDownloadSize">The maximum number of bytes to download.</param>
    /// <param name="bufferSize">The size of the buffer to use when downloading the image.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The uploaded <see cref="Blob"/> or <see langword="null"/> if the operation fails.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="uri"/> is not a valid URI.</exception>
    [SuppressMessage("Documentation", "CSENSE020:Potential ghost parameter reference in documentation", Justification = "Not a ghost reference")]
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Allows for string/Uri overloads")]
    protected async Task<Blob?> DownloadAndUploadImageBlob(
        string uri,
        string? imageMimeType = null,
        long maxDownloadSize = 2000000,
        int bufferSize = 1000000,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uri);

        if (Uri.TryCreate(uri, UriKind.Absolute, out Uri? imageUri))
        {
            return await DownloadAndUploadImageBlob(
                uri: imageUri,
                imageMimeType: imageMimeType,
                maxDownloadSize: maxDownloadSize,
                bufferSize: bufferSize,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        else
        {
            throw new ArgumentException("Invalid URI", nameof(uri));
        }
    }

    /// <summary>
    /// Gets an image from the specified <paramref name="uri"/> and uploads it to blob storage via the <see cref="BlueskyAgent"/>.
    /// </summary>
    /// <param name="uri">The URI of the image to download.</param>
    /// <param name="imageMimeType">The mime type of the image, if known.</param>
    /// <param name="maxDownloadSize">The maximum number of bytes to download.</param>
    /// <param name="bufferSize">The size of the buffer to use when downloading the image.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The uploaded <see cref="Blob"/> or <see langword="null"/> if the operation fails.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="uri"/> is <see langword="null"/>.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown if the agent is not authenticated.</exception>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Error handling")]
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Allows for string/Uri overloads")]
    protected async Task<Blob?> DownloadAndUploadImageBlob(
        Uri uri,
        string? imageMimeType = null,
        long maxDownloadSize = 2000000,
        int bufferSize = 1000000,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (!Agent.IsAuthenticated)
        {
            throw new UnauthorizedAccessException();
        }

        Blob? result = null;

        try
        {
            using (HttpRequestMessage httpRequest = new(HttpMethod.Get, uri))
            {
                httpRequest.Headers.Accept.Clear();
                httpRequest.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("image/jpeg"));
                httpRequest.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("image/png"));
                httpRequest.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("image/gif"));

                using (HttpResponseMessage response = await HttpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        if (response.Content.Headers.ContentLength is not null && response.Content.Headers.ContentLength > maxDownloadSize)
                        {
                            Logger.EmbeddedCardImageTooLarge(ILogger, uri, response.Content.Headers.ContentLength.Value, maxDownloadSize);
                            return null;
                        }

                        using (Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
                        {
                            // Get the first six bytes of the content to check the file signature and prevent downloading large files that aren't images
                            byte[] header = ArrayPool<byte>.Shared.Rent(6);
                            try
                            {
                                int bytesRead = await stream.ReadAsync(header, cancellationToken).ConfigureAwait(false);
                                if (bytesRead < header.Length)
                                {
                                    Logger.EmbeddedCardImageTooSmall(ILogger, uri);
                                    return null;
                                }
                            }
                            finally
                            {
                                ArrayPool<byte>.Shared.Return(header);
                            }

                            imageMimeType ??= SniffImageContentType(header) ?? UnknownImageType;
                            if (imageMimeType == UnknownImageType)
                            {
                                stream.Close();
                                Logger.EmbeddedCardImageTypeNotRecognized(ILogger, uri);
                                return null;
                            }

                            string fileName = Path.GetRandomFileName();
                            byte[] readBuffer = ArrayPool<byte>.Shared.Rent(bufferSize);

                            try
                            {
                                using (var fileStream = new FileStream(fileName, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize, useAsync: true))
                                {
                                    // Write the header bytes we already read
                                    await fileStream.WriteAsync(header, cancellationToken).ConfigureAwait(false);

                                    // Write the rest of the stream to a temporary file
                                    int totalBytesRead = header.Length;
                                    int bytesRead;
                                    while ((bytesRead = await stream.ReadAsync(readBuffer, cancellationToken).ConfigureAwait(false)) > 0)
                                    {
                                        await fileStream.WriteAsync(readBuffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
                                        totalBytesRead += bytesRead;

                                        if (totalBytesRead > maxDownloadSize)
                                        {
                                            fileStream.Close();
                                            Logger.EmbeddedCardImageTooLarge(ILogger, uri, totalBytesRead, maxDownloadSize);
                                            return null;
                                        }
                                    }
                                    await fileStream.FlushAsync(cancellationToken).ConfigureAwait(false);
                                    fileStream.Close();
                                }

                                try
                                {
                                    // Upload the image blob
                                    AtProtoHttpResult<Blob> uploadResult = await Agent.UploadBlob(
                                        fileName: fileName,
                                        mimeType: imageMimeType,
                                        cancellationToken: cancellationToken).ConfigureAwait(false);

                                    if (uploadResult.Succeeded)
                                    {
                                        result = uploadResult.Result;
                                    }
                                    else
                                    {
                                        Logger.EmbeddedCardImageUploadFailed(ILogger, uri, uploadResult.StatusCode, uploadResult.AtErrorDetail?.Error, uploadResult.AtErrorDetail?.Message);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Logger.EmbeddedCardImageUploadThrew(ILogger, uri, ex);
                                }

                            }
                            finally
                            {
                                stream.Close();
                                ArrayPool<byte>.Shared.Return(readBuffer);

                                try
                                {
                                    File.Delete(fileName);
                                }
                                catch (Exception ex)
                                {
                                    Logger.CouldNotDeleteTemporaryFile(ILogger, fileName, ex);
                                }
                            }
                        }
                    }
                    else
                    {
                        Logger.EmbeddedCardGetRequestFailedWithStatusCode(ILogger, uri, response.StatusCode);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.EmbeddedCardImageGetRequestThrew(ILogger, uri, ex);
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
    /// Releases the unmanaged resources used by the <see cref="BaseEmbeddedCardGenerator"/> and optionally disposes of the managed resources.
    /// </summary>
    /// <param name="disposing"><see langword="true"/> to release both managed and unmanaged resources; <see langword="false"/> to releases only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_isDisposed)
        {
            // HttpClient is owned by the caller/agent; do not dispose it here.
            _isDisposed = true;
        }
    }

    /// <summary>
    /// Disposes of any managed and unmanaged resources used by the <see cref="BaseEmbeddedCardGenerator"/>.
    /// </summary>
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
