// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using idunno.AtProto;

namespace idunno.Bluesky.Chat.Group.Model;

internal record CreateGroupRequest
{
    public CreateGroupRequest(ICollection<Did> members, string name)
    {
        ArgumentNullException.ThrowIfNull(members);
        ArgumentOutOfRangeException.ThrowIfZero(members.Count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(members.Count, 49);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(name.Length, 500);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(name.GetGraphemeLength(), 50);

        Members = members;
        Name = name;
    }

    [JsonInclude]
    [JsonRequired]
    public ICollection<Did> Members { get; init; }

    [JsonInclude]
    [JsonRequired]
    public string Name { get; init; }

}