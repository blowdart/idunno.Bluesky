// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using idunno.AtProto;
using idunno.Bluesky.Video;

namespace idunno.Bluesky.Serialization.Test
{
    [ExcludeFromCodeCoverage]
    public class VideoTests
    {
        private readonly JsonSerializerOptions _jsonSerializerOptions = new(JsonSerializerDefaults.Web);

        [Fact]
        public void NotificationDeserializationsFromJson()
        {
            string jsonString = File.ReadAllText(Path.Combine("json", "video", "jobStatus_Created.json"));

            JobStatus? jobStatus = JsonSerializer.Deserialize<JobStatus>(jsonString, _jsonSerializerOptions);

            Assert.NotNull(jobStatus);
            Assert.Equal(JobState.Created, jobStatus.State);
            Assert.Equal(new Did("did:plc:ec72yg6n2sydzjvtovvdlxrk"), jobStatus.Did);
            Assert.Equal("cuog2ca0ours72rbnvgg", jobStatus.JobId);
        }
    }
}
