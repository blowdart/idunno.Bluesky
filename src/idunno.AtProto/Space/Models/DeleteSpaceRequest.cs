// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace idunno.AtProto.Space.Models;

/// <summary>
/// Contains the information required to delete a space.
/// </summary>
[Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
internal sealed record DeleteSpaceRequest([property: JsonRequired] SpaceAtUri Space);
