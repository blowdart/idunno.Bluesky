// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

using idunno.Bluesky.Actor;

namespace idunno.Bluesky.Contact.Model;

internal sealed record GetContactMatchesResponse
{
    [JsonRequired]
    public ICollection<ProfileView> Matches
    {
        get;
        init => field = value ?? throw new JsonException("Contact matches cannot be null.");
    } = [];

    public string? Cursor { get; init; }
}
