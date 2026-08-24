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
    public void StartUploadResponseDeserializesCorrectly()
    {
        string jsonString = """
            {
                "jobId": "da26a5b74lec73akn0b0",
                "partSizeBytes": 5242880,
                "partCount": 1,
                "expiresAt": "2026-08-18T14:58:45.544456319Z"
            }
            """;
        StartUploadResponse? startUploadResponse = JsonSerializer.Deserialize<StartUploadResponse>(jsonString, BlueskyJsonSerializerOptions.Options);
        Assert.NotNull(startUploadResponse);
        Assert.Equal("da26a5b74lec73akn0b0", startUploadResponse.JobId);
        Assert.Equal(5242880, startUploadResponse.PartSize);
        Assert.Equal(1, startUploadResponse.PartCount);
        Assert.Equal(DateTime.Parse("2026-08-18T14:58:45.544456319Z"), startUploadResponse.ExpiresAt);
    }

    [Fact]
    public void UploadPartResponseDeserializesCorrectly()
    {
        string jsonString = """
            {
                "partNumber": 1,
                "sizeBytes": 2848208
            }
            """;
        UploadPartResponse? uploadPartResponse = JsonSerializer.Deserialize<UploadPartResponse>(jsonString, BlueskyJsonSerializerOptions.Options);
        Assert.NotNull(uploadPartResponse);
        Assert.Equal(1, uploadPartResponse.PartNumber);
        Assert.Equal(2848208, uploadPartResponse.Size);
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

    [Fact]
    public void AllGetJobStatusResponsesDeserializeCorrectlyAndConvertToJobStatusCorrectly()
    {
        string jsonString = """
            {
                "jobStatus": {
                    "did": "did:plc:ec72yg6n2sydzjvtovvdlxrk",
                    "jobId": "da26cttf373s73aa5pug",
                    "progress": 0,
                    "state": "JOB_STATE_CREATED"
                }
            }
            """;

        JobStatusResponse? jobStatusResponse = JsonSerializer.Deserialize<JobStatusResponse>(jsonString, BlueskyJsonSerializerOptions.Options);
        Assert.NotNull(jobStatusResponse);
        Assert.Equal("JOB_STATE_CREATED", jobStatusResponse.JobStatus.State);
        Assert.Equal(new Did("did:plc:ec72yg6n2sydzjvtovvdlxrk"), jobStatusResponse.JobStatus.Did);
        Assert.Equal("da26cttf373s73aa5pug", jobStatusResponse.JobStatus.JobId);
        Assert.Equal(0, jobStatusResponse.JobStatus.Progress);
        Assert.Null(jobStatusResponse.JobStatus.Blob);
        Assert.Null(jobStatusResponse.JobStatus.Error);
        Assert.Null(jobStatusResponse.JobStatus.FailureCode);
        Assert.Null(jobStatusResponse.JobStatus.Message);

        JobStatus jobStatus = new(jobStatusResponse.JobStatus);
        Assert.Equal(JobState.Created, jobStatus.State);
        Assert.Equal(new Did("did:plc:ec72yg6n2sydzjvtovvdlxrk"), jobStatus.Did);
        Assert.Equal("da26cttf373s73aa5pug", jobStatus.JobId);
        Assert.Equal(0, jobStatus.Progress);
        Assert.Null(jobStatus.Blob);
        Assert.Null(jobStatus.Error);
        Assert.Null(jobStatus.FailureCode);
        Assert.Null(jobStatus.Message);

        jsonString = """
            {
                "jobStatus": {
                    "did": "did:plc:ec72yg6n2sydzjvtovvdlxrk",
                    "jobId": "d9v2htkbckrc738jq6ng",
                    "progress": 0,
                    "state": "JOB_STATE_ENCODING"
                }
            }
            """;

        jobStatusResponse = JsonSerializer.Deserialize<JobStatusResponse>(jsonString, BlueskyJsonSerializerOptions.Options);
        Assert.NotNull(jobStatusResponse);
        Assert.Equal("JOB_STATE_ENCODING", jobStatusResponse.JobStatus.State);

        jobStatus = new(jobStatusResponse.JobStatus);
        Assert.Equal(JobState.Encoding, jobStatus.State);

        jsonString = """
            {
                "jobStatus": {
                    "did": "did:plc:ec72yg6n2sydzjvtovvdlxrk",
                    "jobId": "d9v2htkbckrc738jq6ng",
                    "progress": 10,
                    "state": "JOB_STATE_ENCODED"
                }
            }
            """;

        jobStatusResponse = JsonSerializer.Deserialize<JobStatusResponse>(jsonString, BlueskyJsonSerializerOptions.Options);
        Assert.NotNull(jobStatusResponse);
        Assert.Equal("JOB_STATE_ENCODED", jobStatusResponse.JobStatus.State);
        Assert.Equal(10, jobStatusResponse.JobStatus.Progress);

        jobStatus = new(jobStatusResponse.JobStatus);
        Assert.Equal(JobState.Encoded, jobStatus.State);
        Assert.Equal(10, jobStatus.Progress);

        jsonString = """
            {
                "jobStatus": {
                    "did": "did:plc:ec72yg6n2sydzjvtovvdlxrk",
                    "jobId": "d9v2htkbckrc738jq6ng",
                    "progress": 20,
                    "state": "JOB_STATE_SCANNING"
                }
            }
            """;

        jobStatusResponse = JsonSerializer.Deserialize<JobStatusResponse>(jsonString, BlueskyJsonSerializerOptions.Options);
        Assert.NotNull(jobStatusResponse);
        Assert.Equal("JOB_STATE_SCANNING", jobStatusResponse.JobStatus.State);
        Assert.Equal(20, jobStatusResponse.JobStatus.Progress);

        jobStatus = new(jobStatusResponse.JobStatus);
        Assert.Equal(JobState.Scanning, jobStatus.State);
        Assert.Equal(20, jobStatus.Progress);

        jsonString = """
            {
                "jobStatus": {
                    "did": "did:plc:ec72yg6n2sydzjvtovvdlxrk",
                    "jobId": "d9v2htkbckrc738jq6ng",
                    "progress": 30,
                    "state": "JOB_STATE_SCANNED"
                }
            }
            """;

        jobStatusResponse = JsonSerializer.Deserialize<JobStatusResponse>(jsonString, BlueskyJsonSerializerOptions.Options);
        Assert.NotNull(jobStatusResponse);
        Assert.Equal("JOB_STATE_SCANNED", jobStatusResponse.JobStatus.State);
        Assert.Equal(30, jobStatusResponse.JobStatus.Progress);

        jobStatus = new(jobStatusResponse.JobStatus);
        Assert.Equal(JobState.Scanned, jobStatus.State);
        Assert.Equal(30, jobStatus.Progress);

        jsonString = """
            {
                "jobStatus": {
                    "did": "did:plc:ec72yg6n2sydzjvtovvdlxrk",
                    "jobId": "d9v2htkbckrc738jq6ng",
                    "progress": 70,
                    "state": "JOB_STATE_UPLOADING"
                }
            }
            """;

        jobStatusResponse = JsonSerializer.Deserialize<JobStatusResponse>(jsonString, BlueskyJsonSerializerOptions.Options);
        Assert.NotNull(jobStatusResponse);
        Assert.Equal("JOB_STATE_UPLOADING", jobStatusResponse.JobStatus.State);
        Assert.Equal(70, jobStatusResponse.JobStatus.Progress);

        jobStatus = new(jobStatusResponse.JobStatus);
        Assert.Equal(JobState.Uploading, jobStatus.State);
        Assert.Equal(70, jobStatus.Progress);

        jsonString = """
            {
                "jobStatus": {
                    "did": "did:plc:ec72yg6n2sydzjvtovvdlxrk",
                    "jobId": "d9v2htkbckrc738jq6ng",
                    "progress": 80,
                    "state": "JOB_STATE_UPLOADED"
                }
            }
            """;

        jobStatusResponse = JsonSerializer.Deserialize<JobStatusResponse>(jsonString, BlueskyJsonSerializerOptions.Options);
        Assert.NotNull(jobStatusResponse);
        Assert.Equal("JOB_STATE_UPLOADED", jobStatusResponse.JobStatus.State);
        Assert.Equal(80, jobStatusResponse.JobStatus.Progress);

        jobStatus = new(jobStatusResponse.JobStatus);
        Assert.Equal(JobState.Uploaded, jobStatus.State);
        Assert.Equal(80, jobStatus.Progress);

        jsonString = """
            {
                "jobStatus": {
                    "blob": {
                        "$type": "blob",
                        "ref": {
                            "$link": "bafkreieveslldimbkmgbp6slaxssu5wq6hnri7cbzwdjv5zknr4e5c376a"
                        },
                        "mimeType": "video/mp4",
                        "size": 4457717
                    },
                    "did": "did:plc:ec72yg6n2sydzjvtovvdlxrk",
                    "jobId": "d9v2htkbckrc738jq6ng",
                    "message": "Video processed successfully",
                    "progress": 100,
                    "state": "JOB_STATE_COMPLETED"
                }
            }
            """;

        jobStatusResponse = JsonSerializer.Deserialize<JobStatusResponse>(jsonString, BlueskyJsonSerializerOptions.Options);
        Assert.NotNull(jobStatusResponse);
        Assert.Equal("JOB_STATE_COMPLETED", jobStatusResponse.JobStatus.State);
        Assert.Equal(100, jobStatusResponse.JobStatus.Progress);
        Assert.Equal("Video processed successfully", jobStatusResponse.JobStatus.Message);
        Assert.NotNull(jobStatusResponse.JobStatus.Blob);
        Assert.Equal(new CidLink("bafkreieveslldimbkmgbp6slaxssu5wq6hnri7cbzwdjv5zknr4e5c376a"), jobStatusResponse.JobStatus.Blob.Reference);
        Assert.Equal("video/mp4", jobStatusResponse.JobStatus.Blob.MimeType);
        Assert.Equal(4457717, jobStatusResponse.JobStatus.Blob.Size);

        jobStatus = new(jobStatusResponse.JobStatus);
        Assert.Equal(JobState.Completed, jobStatus.State);
        Assert.Equal(100, jobStatus.Progress);
        Assert.Equal("Video processed successfully", jobStatus.Message);
        Assert.NotNull(jobStatus.Blob);
        Assert.Equal(new CidLink("bafkreieveslldimbkmgbp6slaxssu5wq6hnri7cbzwdjv5zknr4e5c376a"), jobStatus.Blob.Reference);
        Assert.Equal("video/mp4", jobStatus.Blob.MimeType);
        Assert.Equal(4457717, jobStatus.Blob.Size);

        jsonString = """
            {
                "jobStatus": {
                    "did": "did:plc:ec72yg6n2sydzjvtovvdlxrk",
                    "jobId": "d9v2htkbckrc738jq6ng",
                    "progress": 0,
                    "state": "JOB_STATE_FAILED",
                    "failureCode": "encoding_failure",
                    "error": "notbeans",
                    "message": "really not beans"
                }
            }
            """;

        jobStatusResponse = JsonSerializer.Deserialize<JobStatusResponse>(jsonString, BlueskyJsonSerializerOptions.Options);
        Assert.NotNull(jobStatusResponse);
        Assert.Equal("JOB_STATE_FAILED", jobStatusResponse.JobStatus.State);
        Assert.Equal(0, jobStatusResponse.JobStatus.Progress);
        Assert.Equal("encoding_failure", jobStatusResponse.JobStatus.FailureCode);
        Assert.Equal("notbeans", jobStatusResponse.JobStatus.Error);
        Assert.Equal("really not beans", jobStatusResponse.JobStatus.Message);

        jobStatus = new(jobStatusResponse.JobStatus);
        Assert.Equal(JobState.Failed, jobStatus.State);
        Assert.Equal(0, jobStatus.Progress);
        Assert.Null(jobStatus.Blob);
        Assert.Equal("notbeans", jobStatus.Error);
        Assert.Equal("encoding_failure", jobStatus.FailureCode);
        Assert.Equal("really not beans", jobStatus.Message);
    }
}