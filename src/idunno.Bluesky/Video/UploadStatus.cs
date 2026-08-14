// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.Bluesky.Video.Model;

namespace idunno.Bluesky.Video;

/// <summary>
/// The upload status of a multi-part video upload.
/// </summary>

// This class is used to flatten the wire format into a more usable form for consumers of the library.
// It is not suitable for json deserialization due to the conversion of the state string into an enum.
public sealed record UploadStatus
{
    internal UploadStatus(
        string jobId,
        long partSize,
        long partCount,
        IReadOnlyCollection<int> receivedParts,
        DateTimeOffset expiresAt,
        string state,
        string? completedJobId,
        string? jobStatus,
        string? failureReason)
    {

        JobId = jobId;
        PartSize = partSize;
        PartCount = partCount;
        ReceivedParts = receivedParts;
        ExpiresAt = expiresAt;
        State = state.ToUploadState();
        CompletedJobId = completedJobId;
        JobStatus = jobStatus?.ToJobState();
        FailureReason = failureReason;
    }

    internal UploadStatus(GetUploadStatusResponse getUploadStatusResponse)
        : this(
            jobId: getUploadStatusResponse.JobId,
            partSize: getUploadStatusResponse.PartSize,
            partCount: getUploadStatusResponse.PartCount,
            receivedParts: getUploadStatusResponse.ReceivedParts,
            expiresAt: getUploadStatusResponse.ExpiresAt,
            state: getUploadStatusResponse.State,
            completedJobId: getUploadStatusResponse.CompletedJobId,
            jobStatus: getUploadStatusResponse.JobStatus,
            failureReason: getUploadStatusResponse.FailureReason)
    {
    }

    /// <summary>
    /// Gets the multi-part upload job id. This is used to identify the upload session and is required for all subsequent requests to the upload API.
    /// </summary>
    public string JobId { get; init; }

    /// <summary>
    /// Gets the size of each part in bytes.
    /// </summary>
    public long PartSize { get; init; }

    /// <summary>
    /// Gets the number of parts that make up the complete upload.
    /// </summary>
    public long PartCount { get; init; }

    /// <summary>
    /// Gets the list of part numbers that have been received by the server. This can be used to determine which parts still need to be uploaded.
    /// </summary>
    public IReadOnlyCollection<int> ReceivedParts { get; init; }

    /// <summary>
    /// Gets the expiration date and time of the upload session.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; init; }

    /// <summary>
    /// Gets the current state of the upload session.
    /// </summary>
    public UploadState State { get; init; }

    /// <summary>
    /// Gets the completed job id if the upload has completed successfully. May differ from <see cref="JobId"/> on deduplication.
    /// </summary>
    public string? CompletedJobId { get; init; }

    /// <summary>
    /// Gets the job status if the upload has completed successfully.
    /// </summary>
    public JobState? JobStatus { get; init; }

    /// <summary>
    /// Gets the failure reason if the upload session has failed.
    /// </summary>
    public string? FailureReason { get; init; }
}
