// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Video
{
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
        Created,

        /// <summary>
        /// The video upload is in progress.
        /// </summary>
        InProgress,

        /// <summary>
        /// The video upload has completed successfully.
        /// </summary>
        Completed,

        /// <summary>
        /// The video upload failed.
        /// </summary>
        Failed
    }
}
