// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Buffers;
using System.Text;
using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
    /// The default maximum number of bytes read from a webpage when generating a card from it.
    /// </summary>
    public const int DefaultMaximumPageSize = 1024 * 1024;

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
    /// <param name="maxPageSize">The maximum number of bytes to read from the webpage.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The contents of the webpage as a string or <see langword="null"/> if the request fails.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="uri"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="uri"/> is not a valid http or https URI.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="maxPageSize"/> is zero or negative.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if the generator has been disposed.</exception>
    /// <remarks>
    /// <para>
    ///   The page is served by whoever controls <paramref name="uri"/>, so at most <paramref name="maxPageSize"/> bytes are
    ///   read from it. A longer page is truncated rather than rejected, as the metadata a card is built from appears in the
    ///   document head.
    /// </para>
    /// </remarks>
    [SuppressMessage("Documentation", "CSENSE020:Potential ghost parameter reference in documentation", Justification = "Not a ghost reference")]
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Bounding parameter added with a default to preserve the existing shape")]
    protected virtual async Task<string?> GetPageContent(Uri uri, int maxPageSize = DefaultMaximumPageSize, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxPageSize);

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
                // Read the response headers only, then read a bounded amount of the body. The URI comes from post text,
                // so the server on the other end is untrusted and must not be allowed to dictate the allocation.
                using HttpResponseMessage response = await HttpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    Logger.EmbeddedCardGetRequestFailedWithStatusCode(ILogger, uri, response.StatusCode);
                    return null;
                }

                return await ReadBoundedString(response.Content, maxPageSize, cancellationToken).ConfigureAwait(false);
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
    /// <param name="imageMimeType">The mime type of the image, if known. This is treated as a hint; the type recorded on the blob is always the one sniffed from the content.</param>
    /// <param name="maxDownloadSize">The maximum number of bytes to download.</param>
    /// <param name="bufferSize">The size of the buffer to use when downloading the image.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The uploaded <see cref="Blob"/> or <see langword="null"/> if the operation fails.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="uri"/> is <see langword="null"/>.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown if the agent is not authenticated.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if the generator has been disposed.</exception>
    /// <remarks>
    /// <para>
    ///   <paramref name="imageMimeType"/> typically comes from the page being read, which is served by whoever controls it,
    ///   so it cannot be used to decide what is uploaded. The content is always sniffed and anything which is not a
    ///   recognised image is rejected.
    /// </para>
    /// </remarks>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Error handling")]
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Allows for string/Uri overloads")]
    protected async Task<Blob?> DownloadAndUploadImageBlob(
        Uri uri,
        string? imageMimeType = null,
        long maxDownloadSize = 2000000,
        int bufferSize = 1000000,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        ArgumentNullException.ThrowIfNull(uri);

        if (!Agent.IsAuthenticated)
        {
            throw new UnauthorizedAccessException();
        }

        // The URI comes from the page being read, so it is not necessarily something which should be requested at all.
        if (!uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            Logger.EmbeddedCardImageSchemeNotSupported(ILogger, uri, uri.Scheme);
            return null;
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

                // Read the response headers only. With the default completion option the whole body is buffered into memory
                // before this returns, which would defeat every size check below.
                using (HttpResponseMessage response = await HttpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
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
                            // Don't rent a buffer here as it may return a larger size than requested.
                            byte[] header = new byte[6];
                            int headerBytesRead = await stream.ReadAtLeastAsync(
                                header,
                                header.Length,
                                throwOnEndOfStream: false,
                                cancellationToken: cancellationToken).ConfigureAwait(false);
                            if (headerBytesRead < header.Length)
                            {
                                Logger.EmbeddedCardImageTooSmall(ILogger, uri);
                                return null;
                            }

                            // The declared type is only a hint, and the page which supplied it is untrusted, so the type
                            // recorded on the blob is always the one the content itself says it is.
                            string? sniffedMimeType = SniffImageContentType(header);

                            if (sniffedMimeType is null)
                            {
                                stream.Close();
                                Logger.EmbeddedCardImageTypeNotRecognized(ILogger, uri);
                                return null;
                            }

                            if (imageMimeType is not null &&
                                !imageMimeType.Equals(sniffedMimeType, StringComparison.OrdinalIgnoreCase))
                            {
                                Logger.EmbeddedCardImageTypeMismatch(ILogger, uri, imageMimeType, sniffedMimeType);
                            }

                            imageMimeType = sniffedMimeType;

                            string fileName = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                            byte[] readBuffer = ArrayPool<byte>.Shared.Rent(bufferSize);

                            try
                            {
                                using (var fileStream = new FileStream(fileName, CreateTemporaryFileOptions(bufferSize)))
                                {
                                    // Write the header bytes we already read
                                    await fileStream.WriteAsync(header, cancellationToken).ConfigureAwait(false);

                                    // Write the rest of the stream to a temporary file
                                    int totalBytesRead = header.Length;
                                    int bytesRead;
                                    while ((bytesRead = await stream.ReadAsync(readBuffer, cancellationToken).ConfigureAwait(false)) > 0)
                                    {
                                        // Checked before the write rather than after it, so that a server cannot have an
                                        // extra buffer's worth written to disk by sending it in one read.
                                        if (totalBytesRead + bytesRead > maxDownloadSize)
                                        {
                                            fileStream.Close();
                                            Logger.EmbeddedCardImageTooLarge(ILogger, uri, totalBytesRead + bytesRead, maxDownloadSize);
                                            return null;
                                        }

                                        await fileStream.WriteAsync(readBuffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
                                        totalBytesRead += bytesRead;
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

    /// <summary>
    /// Reads at most <paramref name="maximumLength"/> bytes of <paramref name="content"/> as a string, truncating anything longer.
    /// </summary>
    private static async Task<string> ReadBoundedString(HttpContent content, int maximumLength, CancellationToken cancellationToken)
    {
        // Start small and grow rather than renting the maximum up front, which would allocate it on every request.
        const int initialBufferSize = 8 * 1024;

        int initialLength = content.Headers.ContentLength is long contentLength && contentLength > 0 && contentLength <= maximumLength
            ? (int)Math.Min(contentLength, maximumLength)
            : Math.Min(initialBufferSize, maximumLength);

        byte[] buffer = ArrayPool<byte>.Shared.Rent(initialLength);

        try
        {
            int bytesRead = 0;

            using (Stream contentStream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
            {
                while (bytesRead < maximumLength)
                {
                    if (bytesRead == buffer.Length)
                    {
                        byte[] grown = ArrayPool<byte>.Shared.Rent(Math.Min(buffer.Length * 2, maximumLength));
                        Buffer.BlockCopy(buffer, 0, grown, 0, bytesRead);
                        ArrayPool<byte>.Shared.Return(buffer);
                        buffer = grown;
                    }

                    // The rented buffer may be larger than requested, so bound the read by the requested length rather than by its size.
                    int available = Math.Min(buffer.Length, maximumLength) - bytesRead;

                    int read = await contentStream.ReadAsync(buffer.AsMemory(bytesRead, available), cancellationToken).ConfigureAwait(false);

                    if (read == 0)
                    {
                        break;
                    }

                    bytesRead += read;
                }
            }

            // The page decides how its bytes are encoded, so decode with the charset it declared rather than assuming UTF-8.
            return EncodingFor(content.Headers.ContentType?.CharSet).GetString(buffer, 0, bytesRead);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static string? SniffImageContentType(byte[] imageData)
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

        return null;
    }

    /// <summary>
    /// Creates the options a downloaded image is written to a temporary file with.
    /// </summary>
    private static FileStreamOptions CreateTemporaryFileOptions(int bufferSize)
    {
        FileStreamOptions options = new()
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None,
            BufferSize = bufferSize,
            Options = FileOptions.Asynchronous
        };

        // The temporary directory is shared with every other user of the machine, so the file is created readable only
        // by the user which created it. Setting this on Windows throws, where the ACL inherited from the directory
        // already restricts it.
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        return options;
    }

    /// <summary>
    /// Returns the <see cref="Encoding"/> named by <paramref name="charSet"/>, or <see cref="Encoding.UTF8"/> if it does not name one.
    /// </summary>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Any unusable charset falls back to UTF-8.")]
    private static Encoding EncodingFor(string? charSet)
    {
        if (string.IsNullOrWhiteSpace(charSet))
        {
            return Encoding.UTF8;
        }

        try
        {
            return Encoding.GetEncoding(charSet.Trim().Trim('"'));
        }
        catch (Exception)
        {
            return Encoding.UTF8;
        }
    }

    /// <summary>
    /// Releases the unmanaged resources used by the <see cref="BaseEmbeddedCardGenerator"/> and optionally disposes of the managed resources.
    /// </summary>
    /// <param name="disposing"><see langword="true"/> to release both managed and unmanaged resources; <see langword="false"/> to releases only unmanaged resources.</param>
    /// <remarks>
    /// <para>Once disposed, a generator throws <see cref="ObjectDisposedException"/> rather than making any further requests.</para>
    /// </remarks>
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