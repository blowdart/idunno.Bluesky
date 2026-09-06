// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Actor;
using idunno.Bluesky.Embed;
using idunno.Bluesky.Feed.Gates;
using idunno.Bluesky.Record;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Uploads an image to be referenced in a post.
    /// </summary>
    /// <param name="imageAsBytes">The image, as a byte array.</param>
    /// <param name="mimeType">The mime type of the image. No validation is performed on this value.</param>
    /// <param name="altText">The AltText (for accessibility) for the image.</param>
    /// <param name="aspectRatio">The image's aspect ratio.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="imageAsBytes"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="imageAsBytes"/> is empty.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="mimeType"/> is <see langword="null"/> or empty.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current session is not an authenticated session.</exception>
    public async Task<AtProtoHttpResult<EmbeddedImage>> UploadImage(
        byte[] imageAsBytes,
        string mimeType,
        string altText,
        AspectRatio? aspectRatio,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(imageAsBytes);
        ArgumentOutOfRangeException.ThrowIfZero(imageAsBytes.Length);
        ArgumentException.ThrowIfNullOrEmpty(mimeType);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        AtProtoHttpResult<Blob> uploadResult = await UploadBlob(
            imageAsBytes,
            mimeType,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (uploadResult.Succeeded)
        {
            Logger.ImageUploadSucceed(_logger, Did, uploadResult.Result.Reference.Link);

            return new AtProtoHttpResult<EmbeddedImage>(
                new EmbeddedImage(uploadResult.Result, altText, aspectRatio),
                uploadResult.StatusCode,
                uploadResult.HttpResponseHeaders,
                uploadResult.AtErrorDetail,
                uploadResult.RateLimit);
        }
        else
        {
            Logger.ImageUploadFailed(_logger, uploadResult.StatusCode, Did, uploadResult.AtErrorDetail?.Error, uploadResult.AtErrorDetail?.Message);

            return new AtProtoHttpResult<EmbeddedImage>(
                null,
                uploadResult.StatusCode,
                uploadResult.HttpResponseHeaders,
                uploadResult.AtErrorDetail,
                uploadResult.RateLimit);
        }
    }
}
