// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.AtProto.Repo;

namespace idunno.Bluesky.Video.Model
{
    internal sealed record JobStatusWireFormat
    {
        [JsonConstructor]
        internal JobStatusWireFormat(
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

        [JsonInclude]
        [JsonRequired]
        public string JobId { get; init; }

        [JsonInclude]
        [JsonRequired]
        public Did Did { get; init; }

        [NotNull]
        [JsonInclude]
        public int? Progress { get; init; } = 0;

        [JsonInclude]
        public Blob? Blob { get; init; }

        [JsonInclude]
        public string? Error { get; init; }

        [JsonInclude]
        public string? Message { get; init; }

        [JsonInclude]
        [JsonRequired]
        [JsonPropertyName("state")]
        public string? StateAsString { get; set; }
    }
}
