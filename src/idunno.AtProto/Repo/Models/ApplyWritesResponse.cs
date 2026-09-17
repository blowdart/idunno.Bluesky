// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Repo.Models;

internal sealed record ApplyWritesResponse(Commit Commit, [property: JsonRequired] IReadOnlyCollection<ApplyWritesResponseBase> Results)
{
}