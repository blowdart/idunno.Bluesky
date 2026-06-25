// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Chat.Group.Model;

internal sealed record ListJoinRequestsResponse
{
    [JsonConstructor]
    internal ListJoinRequestsResponse(string? cursor, ICollection<JoinRequestView> requests)
    {
        ArgumentNullException.ThrowIfNull(requests);

        Cursor = cursor;
        Requests = requests;
    }

    public string? Cursor { get; internal init; }

    public ICollection<JoinRequestView> Requests { get; internal init; }
}
