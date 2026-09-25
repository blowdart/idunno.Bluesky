// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Video.Model;

internal sealed record StartUploadRequest(
    [property:JsonRequired] long SizeBytes,
    [property:JsonRequired] string MimeType,
    [property:JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Name,
    [property:JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? DurationMs,
    [property:JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Width,
    [property:JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Height)
{
}
