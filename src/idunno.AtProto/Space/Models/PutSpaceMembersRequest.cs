// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace idunno.AtProto.Space.Models;

/// <summary>
/// Contains a batch of member access changes.
/// </summary>
[Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
internal sealed record PutSpaceMembersRequest(
    [property: JsonRequired] SpaceAtUri Space,
    [property: JsonRequired] IReadOnlyList<SpaceMember> Members);
