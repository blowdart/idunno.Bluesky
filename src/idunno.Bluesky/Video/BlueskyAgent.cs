// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Embed;

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
}