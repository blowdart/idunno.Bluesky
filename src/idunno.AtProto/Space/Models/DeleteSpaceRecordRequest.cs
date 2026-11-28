// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace idunno.AtProto.Space.Models;

/// <summary>
/// Contains the information required to delete a record from a space.
/// </summary>
[Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
internal sealed record DeleteSpaceRecordRequest(
    [property: JsonRequired] SpaceAtUri Space,
    [property: JsonRequired] Did Repo,
    [property: JsonRequired] Nsid Collection,
    [property: JsonRequired][property: JsonPropertyName("rkey")] RecordKey RecordKey);
