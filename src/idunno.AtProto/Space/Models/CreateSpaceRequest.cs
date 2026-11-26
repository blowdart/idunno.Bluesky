// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace idunno.AtProto.Space.Models;

/// <summary>
/// Contains the information required to create a space.
/// </summary>
internal sealed record CreateSpaceRequest(
    [property: JsonRequired] Nsid SpaceType,
    [property: JsonRequired] JsonNode Policy,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RecordKey? Skey);
