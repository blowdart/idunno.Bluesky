// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.AtProto;
using idunno.Bluesky.Video;
using idunno.Bluesky.Video.Model;

namespace idunno.Bluesky.Serialization.Test;

[ExcludeFromCodeCoverage]
public class VideoTests
{
    [Fact]
    public void JobStatusWireFormatDeserializesCreatedJson()
    {
        string jsonString = """
        {
            "did":"did:plc:ec72yg6n2sydzjvtovvdlxrk",
            "jobId":"cuog2ca0ours72rbnvgg",
            "state":"JOB_STATE_CREATED"
        }
        """;

        JobStatusWireFormat? jobStatusWireFormat = JsonSerializer.Deserialize<JobStatusWireFormat>(jsonString, BlueskyJsonSerializerOptions.Options);

        Assert.NotNull(jobStatusWireFormat);
        Assert.Equal("JOB_STATE_CREATED", jobStatusWireFormat.State);
        Assert.Equal("did:plc:ec72yg6n2sydzjvtovvdlxrk", jobStatusWireFormat.Did);
        Assert.Equal("cuog2ca0ours72rbnvgg", jobStatusWireFormat.JobId);

        var jobStatus = new JobStatus(jobStatusWireFormat);

        Assert.Equal(JobState.Created, jobStatus.State);
    }

    [Fact]
    public void JobStatusResponseDeserializesCreatedJson()
    {
        string jsonString = """
        {
            "jobStatus": {
                "did": "did:plc:ec72yg6n2sydzjvtovvdlxrk",
                "jobId": "cuog2ca0ours72rbnvgg",
                "state": "JOB_STATE_CREATED"
            }
        }
        """;

        JobStatusResponse? jobStatusResponse = JsonSerializer.Deserialize<JobStatusResponse>(jsonString, BlueskyJsonSerializerOptions.Options);

        Assert.NotNull(jobStatusResponse);
        Assert.Equal("JOB_STATE_CREATED", jobStatusResponse.JobStatus.State);
        Assert.Equal("did:plc:ec72yg6n2sydzjvtovvdlxrk", jobStatusResponse.JobStatus.Did);
        Assert.Equal("cuog2ca0ours72rbnvgg", jobStatusResponse.JobStatus.JobId);

        var jobStatus = new JobStatus(jobStatusResponse.JobStatus);

        Assert.Equal(JobState.Created, jobStatus.State);
    }

    [Fact]
    public void JobStatusResponseDeserializesAlreadyExistsJson()
    {
        string jsonString = """
        {
            "jobStatus": {
                "did": "did:plc:ec72yg6n2sydzjvtovvdlxrk",
                "error": "already_exists",
                "jobId": "cvcaag996ogs72sgc1p0",
                "message": "Video already processed",
                "state": "JOB_STATE_COMPLETED"
            }
        }
        """;

        JobStatusResponse? jobStatusResponse = JsonSerializer.Deserialize<JobStatusResponse>(jsonString, BlueskyJsonSerializerOptions.Options);

        Assert.NotNull(jobStatusResponse);
        Assert.Equal("JOB_STATE_COMPLETED", jobStatusResponse.JobStatus.State);
        Assert.Equal(new Did("did:plc:ec72yg6n2sydzjvtovvdlxrk"), jobStatusResponse.JobStatus.Did);
        Assert.Equal("cvcaag996ogs72sgc1p0", jobStatusResponse.JobStatus.JobId);
        Assert.Equal("already_exists", jobStatusResponse.JobStatus.Error);
        Assert.Equal("Video already processed", jobStatusResponse.JobStatus.Message);

        JobStatus jobStatus = new(jobStatusResponse.JobStatus);

        Assert.Equal(JobState.Completed, jobStatus.State);
    }

    [Fact]
    public void JobStatusResponseDeserializesFailedJson()
    {
        string jsonString = """
        {
            "jobStatus": {
                "did": "did:plc:ec72yg6n2sydzjvtovvdlxrk",
                "error": "failed",
                "jobId": "cvcaag996ogs72sgc1p0",
                "message": "Video processing failed",
                "state": "JOB_STATE_FAILED"
            }
        }
        """;

        JobStatusResponse? jobStatusResponse = JsonSerializer.Deserialize<JobStatusResponse>(jsonString, BlueskyJsonSerializerOptions.Options);

        Assert.NotNull(jobStatusResponse);
        Assert.Equal("JOB_STATE_FAILED", jobStatusResponse.JobStatus.State);
        Assert.Equal(new Did("did:plc:ec72yg6n2sydzjvtovvdlxrk"), jobStatusResponse.JobStatus.Did);
        Assert.Equal("cvcaag996ogs72sgc1p0", jobStatusResponse.JobStatus.JobId);
        Assert.Equal("failed", jobStatusResponse.JobStatus.Error);
        Assert.Equal("Video processing failed", jobStatusResponse.JobStatus.Message);

        JobStatus jobStatus = new(jobStatusResponse.JobStatus);

        Assert.Equal(JobState.Failed, jobStatus.State);
    }

    [Fact]
    public void JobStatusResponseConversionToJobStatusDefaultsToUnknownWhenDeserializingUndocumentedStatusJson()
    {
        string jsonString = """
        {
            "jobStatus": {
                "did": "did:plc:ec72yg6n2sydzjvtovvdlxrk",
                "jobId": "cvcaag996ogs72sgc1p0",
                "message": "Video processing doing something",
                "state": "JOB_STATE_UNDOCUMENTED"
            }
        }
        """;

        JobStatusResponse? jobStatusResponse = JsonSerializer.Deserialize<JobStatusResponse>(jsonString, BlueskyJsonSerializerOptions.Options);

        Assert.NotNull(jobStatusResponse);
        Assert.Equal("JOB_STATE_UNDOCUMENTED", jobStatusResponse.JobStatus.State);
        Assert.Equal(new Did("did:plc:ec72yg6n2sydzjvtovvdlxrk"), jobStatusResponse.JobStatus.Did);
        Assert.Equal("cvcaag996ogs72sgc1p0", jobStatusResponse.JobStatus.JobId);
        Assert.Null(jobStatusResponse.JobStatus.Error);
        Assert.Equal("Video processing doing something", jobStatusResponse.JobStatus.Message);

        JobStatus jobStatus = new(jobStatusResponse.JobStatus);

        Assert.Equal(JobState.Unknown, jobStatus.State);
    }

    [Fact]
    public void JobStatusResponseWithFailureCodeDeserializesFailedJson()
    {
        string jsonString = """
        {
            "jobStatus": {
                "did": "did:plc:ec72yg6n2sydzjvtovvdlxrk",
                "error": "failed",
                "failureCode": "encoding_failure",
                "jobId": "cvcaag996ogs72sgc1p0",
                "message": "Video processing failed",
                "state": "JOB_STATE_FAILED"
            }
        }
        """;

        JobStatusResponse? jobStatusResponse = JsonSerializer.Deserialize<JobStatusResponse>(jsonString, BlueskyJsonSerializerOptions.Options);

        Assert.NotNull(jobStatusResponse);
        Assert.Equal("JOB_STATE_FAILED", jobStatusResponse.JobStatus.State);
        Assert.Equal(new Did("did:plc:ec72yg6n2sydzjvtovvdlxrk"), jobStatusResponse.JobStatus.Did);
        Assert.Equal("cvcaag996ogs72sgc1p0", jobStatusResponse.JobStatus.JobId);
        Assert.Equal("failed", jobStatusResponse.JobStatus.Error);
        Assert.Equal("Video processing failed", jobStatusResponse.JobStatus.Message);
        Assert.Equal("encoding_failure", jobStatusResponse.JobStatus.FailureCode);

        JobStatus jobStatus = new(jobStatusResponse.JobStatus);

        Assert.Equal(JobState.Failed, jobStatus.State);
    }

    [Fact]
    public void FinishUploadWireResponseDeserializesCorrectly()
    {
        string jsonString = """
            {
                "completedJobId": "da26cttf373s73aa5pug",
                "jobStatus": {
                    "did": "did:plc:ec72yg6n2sydzjvtovvdlxrk",
                    "jobId": "da26cttf373s73aa5pug",
                    "progress": 0,
                    "state": "JOB_STATE_CREATED"
                }
            }
            """;

        FinishUploadWireResponse? finishUploadWireResponse = JsonSerializer.Deserialize<FinishUploadWireResponse>(jsonString, BlueskyJsonSerializerOptions.Options);

        Assert.NotNull(finishUploadWireResponse);
        Assert.Equal("da26cttf373s73aa5pug", finishUploadWireResponse.CompletedJobId);
        Assert.NotNull(finishUploadWireResponse.JobStatus);
        Assert.Equal("JOB_STATE_CREATED", finishUploadWireResponse.JobStatus.State);
        Assert.Equal(new Did("did:plc:ec72yg6n2sydzjvtovvdlxrk"), finishUploadWireResponse.JobStatus.Did);
        Assert.Equal("da26cttf373s73aa5pug", finishUploadWireResponse.JobStatus.JobId);
        Assert.Equal(0, finishUploadWireResponse.JobStatus.Progress);
        Assert.Null(finishUploadWireResponse.JobStatus.Error);
        Assert.Null(finishUploadWireResponse.JobStatus.Message);
        Assert.Null(finishUploadWireResponse.JobStatus.FailureCode);
        Assert.Null(finishUploadWireResponse.JobStatus.Blob);

        var finishUploadResponse = new FinishUploadResponse(finishUploadWireResponse.CompletedJobId, new JobStatus(finishUploadWireResponse.JobStatus));

        Assert.NotNull(finishUploadResponse);
        Assert.Equal("da26cttf373s73aa5pug", finishUploadResponse.CompletedJobId);
        Assert.NotNull(finishUploadResponse.JobStatus);
        Assert.Equal(JobState.Created, finishUploadResponse.JobStatus?.State);
        Assert.Equal(new Did("did:plc:ec72yg6n2sydzjvtovvdlxrk"), finishUploadResponse.JobStatus?.Did);
        Assert.Equal("da26cttf373s73aa5pug", finishUploadResponse.JobStatus?.JobId);
        Assert.Equal(0, finishUploadResponse.JobStatus?.Progress);
        Assert.Null(finishUploadResponse.JobStatus?.Error);
        Assert.Null(finishUploadResponse.JobStatus?.Message);
        Assert.Null(finishUploadResponse.JobStatus?.FailureCode);
        Assert.Null(finishUploadResponse.JobStatus?.Blob);
    }
}