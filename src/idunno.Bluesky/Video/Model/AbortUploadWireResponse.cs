// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Video.Model;

internal sealed record AbortUploadWireResponse(string UploadState, string? CompletedJobId, string? FailureReason)
{
}
