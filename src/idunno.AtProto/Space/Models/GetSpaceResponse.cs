// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace idunno.AtProto.Space.Models;

/// <summary>
/// Contains the response to a get-space request.
/// </summary>
[Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
internal sealed record GetSpaceResponse(
    [property: JsonRequired] SpaceAtUri Space,
    [property: JsonRequired] SpacePolicy Policy);
