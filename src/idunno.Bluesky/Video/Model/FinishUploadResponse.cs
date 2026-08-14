// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Video.Model;

internal record FinishUploadResponse(
    string JobId,
    string? JobStatus)
{
}
