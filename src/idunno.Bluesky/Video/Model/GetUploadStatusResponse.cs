// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Video.Model;

internal record GetUploadStatusResponse(
    [property: JsonRequired] string JobId,
    [property: JsonRequired, JsonPropertyName("partSizeBytes")] long PartSize,
    [property: JsonRequired] long PartCount,
    [property: JsonRequired] IReadOnlyCollection<int> ReceivedParts,
    [property: JsonRequired] DateTimeOffset ExpiresAt,
    [property: JsonRequired] string State,
    string? CompletedJobId,
    JobStatus? JobStatus,
    string? FailureReason)
{
}