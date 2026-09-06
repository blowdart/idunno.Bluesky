// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Video.Model;

internal sealed record JobStatusResponse(
    [property: JsonRequired] JobStatusWireFormat JobStatus)
{
}