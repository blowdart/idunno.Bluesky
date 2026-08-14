// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Video;

/// <summary>
/// Represents the state of a video upload.
/// </summary>
public enum JobState
{
    /// <summary>
    /// The job state returned cannot be mapped.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// The video processing job was created
    /// </summary>
    Created = 1,

    /// <summary>
    /// The video is being processed.
    /// </summary>
    InProgress = 2,

    /// <summary>
    /// The video upload has completed successfully.
    /// </summary>
    Completed = 3,

    /// <summary>
    /// The video upload failed.
    /// </summary>
    Failed = 4,

    /// <summary>
    /// The video upload is being uploaded.
    /// </summary>
    Uploading = 5,

    /// <summary>
    /// The video is being encoded.
    /// </summary>
    Encoding = 6,

    /// <summary>
    /// The video has been encoded.
    /// </summary>
    Encoded = 7,

    /// <summary>
    /// The video is being scanned.
    /// </summary>
    Uploaded = 8,

    /// <summary>
    /// The video has been scanned.
    /// </summary>
    Scanned = 9,

    /// <summary>
    /// The video is been uploaded.
    /// </summary>
    Scanning = 10,
}

internal static class JobStateExtensions
{
    /// <summary>
    /// Converts the specified string to a <see cref="JobState"/>.
    /// </summary>
    /// <param name="jobState">The job state string to convert.</param>
    /// <returns>The corresponding <see cref="JobState"/>.</returns>
    public static JobState ToJobState(this string jobState)
    {
        return jobState.ToUpperInvariant() switch
        {
            "JOB_STATE_CREATED" => JobState.Created,
            "JOB_STATE_ENCODING" => JobState.Encoding,
            "JOB_STATE_ENCODED" => JobState.Encoded,
            "JOB_STATE_SCANNING" => JobState.Scanning,
            "JOB_STATE_SCANNED" => JobState.Scanned,
            "JOB_STATE_UPLOADING" => JobState.Uploading,
            "JOB_STATE_UPLOADED" => JobState.Uploaded,
            "JOB_STATE_COMPLETED" => JobState.Completed,
            "JOB_STATE_FAILED" => JobState.Failed,
            "JOB_STATE_IN_PROGRESS" => JobState.InProgress, // Old state, kept for backwards compatibility
            _ => JobState.Unknown,
        };
    }
}