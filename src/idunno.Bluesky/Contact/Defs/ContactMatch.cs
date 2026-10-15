// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

using idunno.Bluesky.Actor;

#pragma warning disable IDE0130
namespace idunno.Bluesky.Contact;
#pragma warning restore IDE0130

/// <summary>
/// Associates a matched profile with its position in the imported contact list.
/// </summary>
public sealed record ContactMatch : View
{
    [JsonConstructor]
    internal ContactMatch(ProfileView match, int contactIndex)
    {
        if (match is null || contactIndex is < 0 or > 999)
        {
            throw new JsonException("A contact match requires a profile and an index between 0 and 999.");
        }

        Match = match;
        ContactIndex = contactIndex;
    }

    /// <summary>
    /// Gets the matched profile.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public ProfileView Match { get; internal init; }

    /// <summary>
    /// Gets the zero-based index in the contact list supplied to the import.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public int ContactIndex { get; internal init; }
}
