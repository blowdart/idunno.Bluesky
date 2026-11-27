// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace idunno.AtProto.Space.Models;

/// <summary>
/// Contains a batch of member DIDs to remove.
/// </summary>
[Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
internal sealed record RemoveSpaceMembersRequest(
    [property: JsonRequired] SpaceAtUri Space,
    [property: JsonRequired] IReadOnlyList<Did> Dids);
