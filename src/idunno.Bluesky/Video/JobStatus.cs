// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.AtProto.Repo;

namespace idunno.Bluesky.Video
{
    /// <summary>
    /// Provides the status of a video upload.
    /// </summary>
    public sealed record JobStatus
    {
        [JsonConstructor]
        internal JobStatus(
            string jobId,
            Did did,
            string? stateAsString,
            int? progress,
            Blob? blob,
            string? error,
            string? message)
        {
            JobId = jobId;
            Did = did;
            StateAsString = stateAsString;
            Blob = blob;
            Error = error;
            Message = message;

            if (progress is not null)
            {
                Progress = progress;
            }
        }

        /// <summary>
        /// Gets the job identifier.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public string JobId { get; init; }

        /// <summary>
        /// Gets the <see cref="AtProto.Did"/> the job belongs to.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public Did Did { get; init; }

        /// <summary>
        /// Gets the current <see cref="JobState"/>.
        /// </summary>
        [JsonIgnore]
        public JobState State {
            get
            {
                // Shortcut the most common state.
                if (StateAsString is null)
                {
                    return JobState.InProgress;
                }

                if (string.Equals(StateAsString, "JOB_STATE_COMPLETED", StringComparison.Ordinal))
                {
                    return JobState.Completed;
                }
                else if (string.Equals(StateAsString, "JOB_STATE_FAILED", StringComparison.Ordinal))
                {
                    return JobState.Failed;
                }
                else if (string.Equals(StateAsString, "JOB_STATE_CREATED", StringComparison.Ordinal))
                {
                    return JobState.Created;
                }
                else
                {
                    return JobState.InProgress;
                }
            }
        }

        /// <summary>
        /// Gets the progress of the job.
        /// </summary>
        [NotNull]
        [JsonInclude]
        public int? Progress { get; init; } = 0;

        /// <summary>
        /// Gets a reference to the <see cref="Blob"/> containing the video if <see cref="State"/> is <see cref="JobState.Completed"/>.
        /// </summary>
        [JsonInclude]
        public Blob? Blob { get; init; }

        /// <summary>
        /// Gets a description of any error that happened during processing.
        /// </summary>
        [JsonInclude]
        public string? Error { get; init; }

        /// <summary>
        /// Gets a description of any error that happened during processing.
        /// </summary>
        [JsonInclude]
        public string? Message { get; init; }

        [JsonInclude]
        [JsonRequired]
        [JsonPropertyName("state")]
        private string? StateAsString { get; set; }
    }

    /// <summary>
    /// Represents the state of a video upload.
    /// </summary>
    public enum JobState
    {
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
