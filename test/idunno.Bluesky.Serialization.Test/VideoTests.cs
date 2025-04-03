// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.AtProto;
using idunno.Bluesky.Video;
using idunno.Bluesky.Video.Model;

namespace idunno.Bluesky.Serialization.Test
{
    [ExcludeFromCodeCoverage]
    public class VideoTests
    {
        private readonly JsonSerializerOptions _jsonSerializerOptions = new(JsonSerializerDefaults.Web);

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

            JobStatusWireFormat? jobStatusWireFormat = JsonSerializer.Deserialize<JobStatusWireFormat>(jsonString, _jsonSerializerOptions);

            Assert.NotNull(jobStatusWireFormat);
            Assert.Equal("JOB_STATE_CREATED", jobStatusWireFormat.StateAsString);
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

            JobStatusResponse? jobStatusResponse = JsonSerializer.Deserialize<JobStatusResponse>(jsonString, _jsonSerializerOptions);

            Assert.NotNull(jobStatusResponse);
            Assert.Equal("JOB_STATE_CREATED", jobStatusResponse.JobStatus.StateAsString);
            Assert.Equal("did:plc:ec72yg6n2sydzjvtovvdlxrk", jobStatusResponse.JobStatus.Did);
            Assert.Equal("cuog2ca0ours72rbnvgg", jobStatusResponse.JobStatus.JobId);

            var jobStatus = new JobStatus(jobStatusResponse.JobStatus);

            Assert.Equal(JobState.Created, jobStatus.State);
        }

        [Fact]
        public void JobStatusWireFormatDeserializesAlreadyExistsJson()
        {
            string jsonString = """
            {
                "did": "did:plc:ec72yg6n2sydzjvtovvdlxrk",
                "error": "already_exists",
                "jobId": "cvcaag996ogs72sgc1p0",
                "message": "Video already processed",
                "state": "JOB_STATE_COMPLETED"
            }
            """;

            JobStatusWireFormat? jobStatusWireFormat = JsonSerializer.Deserialize<JobStatusWireFormat>(jsonString, _jsonSerializerOptions);

            Assert.NotNull(jobStatusWireFormat);
            Assert.Equal("JOB_STATE_COMPLETED", jobStatusWireFormat.StateAsString);
            Assert.Equal("did:plc:ec72yg6n2sydzjvtovvdlxrk", jobStatusWireFormat.Did);
            Assert.Equal("cvcaag996ogs72sgc1p0", jobStatusWireFormat.JobId);
            Assert.Equal("already_exists", jobStatusWireFormat.Error);
            Assert.Equal("Video already processed", jobStatusWireFormat.Message);

            var jobStatus = new JobStatus(jobStatusWireFormat);

            Assert.Equal(JobState.Completed, jobStatus.State);
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

            JobStatusResponse? jobStatusResponse = JsonSerializer.Deserialize<JobStatusResponse>(jsonString, _jsonSerializerOptions);

            Assert.NotNull(jobStatusResponse);
            Assert.Equal("JOB_STATE_COMPLETED", jobStatusResponse.JobStatus.StateAsString);
            Assert.Equal(new Did("did:plc:ec72yg6n2sydzjvtovvdlxrk"), jobStatusResponse.JobStatus.Did);
            Assert.Equal("cvcaag996ogs72sgc1p0", jobStatusResponse.JobStatus.JobId);
            Assert.Equal("already_exists", jobStatusResponse.JobStatus.Error);
            Assert.Equal("Video already processed", jobStatusResponse.JobStatus.Message);

            var jobStatus = new JobStatus(jobStatusResponse.JobStatus);

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

            JobStatusResponse? jobStatusResponse = JsonSerializer.Deserialize<JobStatusResponse>(jsonString, _jsonSerializerOptions);

            Assert.NotNull(jobStatusResponse);
            Assert.Equal("JOB_STATE_FAILED", jobStatusResponse.JobStatus.StateAsString);
            Assert.Equal(new Did("did:plc:ec72yg6n2sydzjvtovvdlxrk"), jobStatusResponse.JobStatus.Did);
            Assert.Equal("cvcaag996ogs72sgc1p0", jobStatusResponse.JobStatus.JobId);
            Assert.Equal("failed", jobStatusResponse.JobStatus.Error);
            Assert.Equal("Video processing failed", jobStatusResponse.JobStatus.Message);

            var jobStatus = new JobStatus(jobStatusResponse.JobStatus);

            Assert.Equal(JobState.Failed, jobStatus.State);
        }

        [Fact]
        public void JobStatusResponseDefaultsToInProgressWhenDeserializingUnkdocumentaedStatusJson()
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

            JobStatusResponse? jobStatusResponse = JsonSerializer.Deserialize<JobStatusResponse>(jsonString, _jsonSerializerOptions);

            Assert.NotNull(jobStatusResponse);
            Assert.Equal("JOB_STATE_UNDOCUMENTED", jobStatusResponse.JobStatus.StateAsString);
            Assert.Equal(new Did("did:plc:ec72yg6n2sydzjvtovvdlxrk"), jobStatusResponse.JobStatus.Did);
            Assert.Equal("cvcaag996ogs72sgc1p0", jobStatusResponse.JobStatus.JobId);
            Assert.Null(jobStatusResponse.JobStatus.Error);
            Assert.Equal("Video processing doing something", jobStatusResponse.JobStatus.Message);

            var jobStatus = new JobStatus(jobStatusResponse.JobStatus);

            Assert.Equal(JobState.InProgress, jobStatus.State);
        }
    }
}
