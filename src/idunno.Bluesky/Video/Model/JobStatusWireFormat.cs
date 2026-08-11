// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using idunno.AtProto;

namespace idunno.Bluesky.Video.Model;

[JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = true, UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(JobStatusWireFormat), "app.bsky.video.defs#jobStatus")]
internal record JobStatusWireFormat
{
    [JsonConstructor]
    internal JobStatusWireFormat(
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
        State = state;
        Blob = blob;
        Error = error;
        Message = message;
        FailureCode = failureCode;

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
    public string? State { get; init; }

    [JsonInclude]
    public string? FailureCode { get; init; }
}