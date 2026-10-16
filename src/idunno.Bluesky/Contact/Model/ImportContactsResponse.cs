// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace idunno.Bluesky.Contact.Model;

internal sealed record ImportContactsResponse
{
    [JsonRequired]
    public ICollection<ContactMatch> MatchesAndContactIndexes
    {
        get;
        init => field = value ?? throw new JsonException("Imported contact matches cannot be null.");
    } = [];
}
