// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Video.Model
{
    internal sealed record JobStatusResponse
    {
        [JsonConstructor]
        public JobStatusResponse(JobStatusWireFormat jobStatus)
        {
            JobStatus = jobStatus;
        }

        [JsonInclude]
        public JobStatusWireFormat JobStatus { get; init; }
    }
}
