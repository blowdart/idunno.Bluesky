// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Embed;
using idunno.Bluesky.Video;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    private readonly Uri _videoServer = new("https://video.bsky.app/");

    private readonly Did _videoAudience = new("did:web:video.bsky.app");

    private const string UploadBlobLxm = "com.atproto.repo.uploadBlob";

    /// <summary>
    /// Uploads a caption file to be referenced in an embedded video.
    /// </summary>
    /// <param name="captionsAsBytes">The captions, as a byte array.</param>
    /// <param name="captionLanguage">The language the captions are in.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="captionsAsBytes"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="captionLanguage"/> is <see langword="null"/> or empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="captionsAsBytes"/> is a zero length array.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current session is not an authenticated session.</exception>
    public async Task<AtProtoHttpResult<Caption>> UploadCaptions(
        byte[] captionsAsBytes,
        string captionLanguage,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(captionsAsBytes);
        ArgumentException.ThrowIfNullOrEmpty(captionLanguage);
        ArgumentOutOfRangeException.ThrowIfZero(captionsAsBytes.Length);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        AtProtoHttpResult<Blob> uploadResult = await UploadBlob(
            captionsAsBytes,
            "text/vtt",
            cancellationToken: cancellationToken).ConfigureAwait(false);


        if (uploadResult.Succeeded)
        {
            return new AtProtoHttpResult<Caption>(
                new Caption(captionLanguage, uploadResult.Result),
                uploadResult.StatusCode,
                uploadResult.HttpResponseHeaders,
                uploadResult.AtErrorDetail,
                uploadResult.RateLimit);
        }
        else
        {
            return new AtProtoHttpResult<Caption>(
                null,
                uploadResult.StatusCode,
                uploadResult.HttpResponseHeaders,
                uploadResult.AtErrorDetail,
                uploadResult.RateLimit);
        }
    }

    /// <summary>
    /// Uploads an animated gif to be processed and stored.
    /// </summary>
    /// <param name="fileName">The filename of the gif.</param>
    /// <param name="gif">The gif to upload as bytes.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="fileName"/> is <see langword="null"/> or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="gif"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="gif"/> is empty.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    [Obsolete("Use UploadVideo(string fileName, byte[] media, string mimeType, CancellationToken cancellationToken) with a mimeType of video/gif instead.")]
    public async Task<AtProtoHttpResult<JobStatus>> UploadAnimatedGif(
        string fileName,
        byte[] gif,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(fileName);
        ArgumentNullException.ThrowIfNull(gif);
        ArgumentOutOfRangeException.ThrowIfZero(gif.Length);

        // The mime type for animated gifs is "image/gif". Even though UploadVideo was for video, the server will accept an animated gif as a video if the mime type is set to "image/gif".
        return await UploadVideo(fileName, gif, "image/gif", cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Uploads video to be processed and stored.
    /// </summary>
    /// <param name="fileName">The filename of the media.</param>
    /// <param name="video">The video to upload as bytes.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="fileName"/> is <see langword="null"/> or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="video"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="video"/> is empty.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    [Obsolete("Use UploadVideo(string fileName, byte[] media, string mimeType, CancellationToken cancellationToken) with a mimeType of video/mp4 instead.")]
    public async Task<AtProtoHttpResult<JobStatus>> UploadVideo(
        string fileName,
        byte[] video,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(fileName);
        ArgumentNullException.ThrowIfNull(video);
        ArgumentOutOfRangeException.ThrowIfZero(video.Length);

        return await UploadVideo(fileName, video, "video/mp4", cancellationToken).ConfigureAwait(false);
    }
}