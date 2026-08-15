// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Video.Model;

internal record FinishUploadWireResponse(
    [property: JsonRequired] string CompletedJobId,
    [property: JsonRequired] string JobStatus)
{
}
