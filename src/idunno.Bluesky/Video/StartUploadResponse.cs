// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Video;

/// <summary>
/// Encapsulates the response to a request to start a multipart video upload.
/// </summary>
/// <param name="JobId">The job ID to use when uploading video parts.</param>
/// <param name="PartSize">The size of each part should be in bytes.</param>
/// <param name="PartCount">The total number of parts.</param>
/// <param name="ExpiresAt">The expiration date and time when the upload job expires and no longer accepts new parts.</param>
public sealed record StartUploadResponse(
    [property: JsonRequired] string JobId,
    [property: JsonRequired, JsonPropertyName("partSizeBytes")] int PartSize,
    [property: JsonRequired] int PartCount,
    [property: JsonRequired] DateTimeOffset ExpiresAt)
{
}
