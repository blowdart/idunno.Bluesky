// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.Bluesky.Video.Model;

namespace idunno.Bluesky.Video;

/// <summary>
/// Provides the status of a video upload.
/// </summary>

// This class is used to flatten the wire format into a more usable form for consumers of the library.
// It is not suitable for json deserialization due to the conversion of the state string into an enum.
public sealed record JobStatus
{
    internal JobStatus(
        string jobId,
        Did did,
        string? state,
        int? progress,
        Blob? blob,
        string? error,
        string? message,
        string? failureCode)
    {
        JobId = jobId;
        Did = did;
        Blob = blob;
        Error = error;
        Message = message;
        FailureCode = failureCode;
        RawState = state;

        if (state is not null)
        {
            State = state.ToJobState();
        }

        if (progress is not null)
        {
            Progress = (int)progress;
        }
    }

    internal JobStatus(JobStatusResponse jobStatusResponse)
        : this(
            jobId: jobStatusResponse.JobStatus.JobId,
            did: jobStatusResponse.JobStatus.Did,
            state: jobStatusResponse.JobStatus.State,
            progress: jobStatusResponse.JobStatus.Progress,
            blob: jobStatusResponse.JobStatus.Blob,
            error: jobStatusResponse.JobStatus.Error,
            message: jobStatusResponse.JobStatus.Message,
            failureCode: jobStatusResponse.JobStatus.FailureCode)
    {
    }

    internal JobStatus(JobStatusWireFormat jobStatusWireFormat)
        : this(
            jobId: jobStatusWireFormat.JobId,
            did: jobStatusWireFormat.Did,
            state: jobStatusWireFormat.State,
            progress: jobStatusWireFormat.Progress,
            blob: jobStatusWireFormat.Blob,
            error: jobStatusWireFormat.Error,
            message: jobStatusWireFormat.Message,
            failureCode: jobStatusWireFormat.FailureCode)
    {
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
    [JsonIgnore]
    public JobState State { get; init; } = JobState.Unknown;

    /// <summary>
    /// Gets the state of the job, as the string returned from the API.
    /// </summary>
    public string? RawState { get; init; }

    /// <summary>
    /// Gets the progress of the job.
    /// </summary>
    public int Progress { get; init; }

    /// <summary>
    /// Gets a reference to the <see cref="Blob"/> containing the video if <see cref="State"/> is <see cref="JobState.Completed"/>.
    /// </summary>
    public Blob? Blob { get; init; }

    /// <summary>
    /// Gets a description of any error that happened during processing.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// An optional machine-readable code for why the video processing job failed. Known values are defined in <see cref="FailureCodes"/>.
    /// </summary>
    public string? FailureCode { get; init; }

    /// <summary>
    /// Gets a description of any error that happened during processing.
    /// </summary>
    public string? Message { get; init; }
}