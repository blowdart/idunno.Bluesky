// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Video.Model;

namespace idunno.Bluesky.Video
{
    /// <summary>
    /// Provides the status of a video upload.
    /// </summary>
    public sealed record JobStatus
    {
        internal JobStatus(JobStatusWireFormat jobStatusWireFormat)
        {
            JobId = jobStatusWireFormat.JobId;
            Did = jobStatusWireFormat.Did;
            Blob = jobStatusWireFormat.Blob;
            Error = jobStatusWireFormat.Error;
            Message = jobStatusWireFormat.Message;

            if (jobStatusWireFormat.StateAsString is not null)
            {
                switch (jobStatusWireFormat.StateAsString.ToUpperInvariant())
                {
                    case "JOB_STATE_COMPLETED":
                        State = JobState.Completed;
                        break;

                    case "JOB_STATE_FAILED":
                        State = JobState.Failed;
                        break;

                    case "JOB_STATE_CREATED":
                        State = JobState.Created;
                        break;
                }
            }

            if (jobStatusWireFormat.Progress is not null)
            {
                Progress = (int)jobStatusWireFormat.Progress;
            }
        }

        /// <summary>
        /// Gets the job identifier.
        /// </summary>
        public string JobId { get; init; }

        /// <summary>
        /// Gets the <see cref="AtProto.Did"/> the job belongs to.
        /// </summary>
        public Did Did { get; init; }

        /// <summary>
        /// Gets the current <see cref="JobState"/>.
        /// </summary>
        public JobState State { get; init; } = JobState.InProgress;

        /// <summary>
        /// Gets the progress of the job.
        /// </summary>
        [NotNull]
        public int Progress { get; init; } = 0;

        /// <summary>
        /// Gets a reference to the <see cref="Blob"/> containing the video if <see cref="State"/> is <see cref="JobState.Completed"/>.
        /// </summary>
        public Blob? Blob { get; init; }

        /// <summary>
        /// Gets a description of any error that happened during processing.
        /// </summary>
        public string? Error { get; init; }

        /// <summary>
        /// Gets a description of any error that happened during processing.
        /// </summary>
        public string? Message { get; init; }
    }
}
