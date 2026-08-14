// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.Bluesky.Video.Model;

namespace idunno.Bluesky.Video;

/// <summary>
/// Encapsulates the response from an abort upload request. This is used to determine if the abort was successful and the state of the upload after the abort request.
/// </summary>
public sealed record AbortUploadResponse
{
    internal AbortUploadResponse(AbortUploadWireResponse response)
    {
        State = response.UploadState.ToUploadState();
        CompletedJobId = response.CompletedJobId;
        FailureReason = response.FailureReason;
    }

    /// <summary>
    /// Gets the current state of the upload after the abort request. This can be used to determine if the abort was successful or if the upload has already completed or failed.
    /// </summary>
    public UploadState State { get; init; }

    /// <summary>
    /// Gets the completed job id if the upload had already completed. Present only if the state is <see cref="UploadState.Completed"/>.
    /// </summary>
    public string? CompletedJobId { get; init; }

    /// <summary>
    /// Gets the failure reason if the upload had already failed. Present only if the state is <see cref="UploadState.Failed"/>.
    /// </summary>
    public string? FailureReason { get; init; }
}
