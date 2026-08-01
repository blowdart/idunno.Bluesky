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
    Encoding = 6
}