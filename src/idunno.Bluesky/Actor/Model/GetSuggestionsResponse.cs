// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace idunno.Bluesky.Actor.Model;

[SuppressMessage("Performance", "CA1812", Justification = "Used in GetSuggestions.")]
internal sealed record GetSuggestionsResponse
{
    [JsonConstructor]
    public GetSuggestionsResponse(string? cursor, IReadOnlyCollection<ProfileView> actors, string? recIdStr)
    {
        Cursor = cursor;
        Actors = actors;
        RecIdStr = recIdStr;
    }

    [JsonInclude]
    public string? Cursor { get; init; }

    [JsonInclude]
    [JsonRequired]
    public IReadOnlyCollection<ProfileView> Actors { get; init; }

    [JsonInclude]
    public string? RecIdStr { get; init; }
}