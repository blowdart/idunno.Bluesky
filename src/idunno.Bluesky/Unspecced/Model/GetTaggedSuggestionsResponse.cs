// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Unspecced.Model;

internal sealed record GetTaggedSuggestionsResponse([property: JsonRequired] ICollection<Suggestion> Suggestions)
{
}