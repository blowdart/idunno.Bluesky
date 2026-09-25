// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;

namespace idunno.Bluesky.Chat.Group.Model;

internal record CreateGroupRequest
{
    public CreateGroupRequest(ICollection<Did> members, string name)
    {
        ArgumentNullException.ThrowIfNull(members);
        ArgumentOutOfRangeException.ThrowIfZero(members.Count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(members.Count, Maximum.GroupMembers);
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(name.GetUtf8Length(), Maximum.GroupNameLengthInBytes);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(name.GetGraphemeLength(), Maximum.GroupNameLengthInGraphemes);

        Members = new List<Did>(members);
        Name = name;
    }

    [JsonInclude]
    [JsonRequired]
    public ICollection<Did> Members { get; init; }

    [JsonInclude]
    [JsonRequired]
    public string Name { get; init; }

}