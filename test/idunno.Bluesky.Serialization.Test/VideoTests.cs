// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
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
        public void JobStatusDeserializesCreatedJson()
        {
            string jsonString = File.ReadAllText(Path.Combine("json", "video", "jobStatus_Created.json"));

            JobStatus? jobStatus = JsonSerializer.Deserialize<JobStatus>(jsonString, _jsonSerializerOptions);

            Assert.NotNull(jobStatus);
            Assert.Equal(JobState.Created, jobStatus.State);
            Assert.Equal(new Did("did:plc:ec72yg6n2sydzjvtovvdlxrk"), jobStatus.Did);
            Assert.Equal("cuog2ca0ours72rbnvgg", jobStatus.JobId);
        }

        [Fact]
        public void JobStatusResponseCreatedJson()
        {
            string jsonString = File.ReadAllText(Path.Combine("json", "video", "getJobStatus_Created.json"));

            JobStatusResponse? jobStatusResponse = JsonSerializer.Deserialize<JobStatusResponse>(jsonString, _jsonSerializerOptions);

            Assert.NotNull(jobStatusResponse);
            Assert.Equal(JobState.Created, jobStatusResponse.JobStatus.State);
            Assert.Equal(new Did("did:plc:ec72yg6n2sydzjvtovvdlxrk"), jobStatusResponse.JobStatus.Did);
            Assert.Equal("cuog2ca0ours72rbnvgg", jobStatusResponse.JobStatus.JobId);
        }

        [Fact]
        public void JobStatusDeserializesAlreadyExistsJson()
        {
            string jsonString = File.ReadAllText(Path.Combine("json", "video", "jobStatus_AlreadyExists.json"));

            JobStatus? jobStatus = JsonSerializer.Deserialize<JobStatus>(jsonString, _jsonSerializerOptions);

            Assert.NotNull(jobStatus);
            Assert.Equal(JobState.Completed, jobStatus.State);
            Assert.Equal(new Did("did:plc:ec72yg6n2sydzjvtovvdlxrk"), jobStatus.Did);
            Assert.Equal("cvcaag996ogs72sgc1p0", jobStatus.JobId);
            Assert.Equal("already_exists", jobStatus.Error);
            Assert.Equal("Video already processed", jobStatus.Message);
        }

        [Fact]
        public void JobStatusResponseDeserializesAlreadyExistsJson()
        {
            string jsonString = File.ReadAllText(Path.Combine("json", "video", "getJobStatus_AlreadyExists.json"));

            JobStatusResponse? jobStatusResponse = JsonSerializer.Deserialize<JobStatusResponse>(jsonString, _jsonSerializerOptions);

            Assert.NotNull(jobStatusResponse);
            Assert.Equal(JobState.Completed, jobStatusResponse.JobStatus.State);
            Assert.Equal(new Did("did:plc:ec72yg6n2sydzjvtovvdlxrk"), jobStatusResponse.JobStatus.Did);
            Assert.Equal("cvcaag996ogs72sgc1p0", jobStatusResponse.JobStatus.JobId);
            Assert.Equal("already_exists", jobStatusResponse.JobStatus.Error);
            Assert.Equal("Video already processed", jobStatusResponse.JobStatus.Message);
        }

    }
}
