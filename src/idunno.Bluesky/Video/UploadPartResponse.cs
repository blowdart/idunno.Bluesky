// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Video;

/// <summary>
/// Represents the response from the server when uploading a part of a multi-part video upload.
/// </summary>
/// <param name="PartNumber">The part number of the uploaded video part.</param>
/// <param name="Size">The size of the uploaded video part in bytes.</param>
public sealed record UploadPartResponse(
    [property: JsonRequired] long PartNumber,
    [property: JsonRequired, JsonPropertyName("sizeBytes")] long Size)
{
}
