// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Video;

/// <summary>
/// Encapsulates the response from the FinishUpload operation, containing the completed job ID and the job status.
/// </summary>
/// <param name="CompletedJobId">The processing job to poll with getJobStatus; on deduplication this may differ from the input jobId.</param>
/// <param name="JobStatus">The status of the completed job.</param>
public sealed record FinishUploadResponse(
    string CompletedJobId,
    JobStatus? JobStatus)
{
}
