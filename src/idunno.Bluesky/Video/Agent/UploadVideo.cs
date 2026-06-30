// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Video;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Uploads video to be processed and stored.
    /// </summary>
    /// <param name="fileName">The filename of the media.</param>
    /// <param name="media">The media to upload as bytes.</param>
    /// <param name="mimeType">The MIME type of the media. Must start with 'video/' or be 'image/gif'.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="fileName"/> is <see langword="null"/> or empty, or when <paramref name="mimeType"/> does not start with 'video/' or is not 'image/gif'.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="media"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="media"/> is empty.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<JobStatus>> UploadVideo(
        string fileName,
        byte[] media,
        string mimeType,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(fileName);
        ArgumentNullException.ThrowIfNull(media);
        ArgumentException.ThrowIfNullOrWhiteSpace(mimeType);
        ArgumentOutOfRangeException.ThrowIfZero(media.Length);

        if (!mimeType.StartsWith("video/", StringComparison.OrdinalIgnoreCase) &&
            !mimeType.Equals("image/gif", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The MIME type must start with 'video/' or be 'image/gif'.", nameof(mimeType));
        }


        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await UploadMedia(fileName, media, mimeType, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Uploads a video to be processed and stored.
    /// </summary>
    /// <param name="fileName">The filename of the video.</param>
    /// <param name="video">The video to upload as bytes.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="fileName"/> is <see langword="null"/> or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="video"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="video"/> is empty.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    [Obsolete("Use UploadVideo(string fileName, byte[] media, string mimeType, CancellationToken cancellationToken) instead.", false)]
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