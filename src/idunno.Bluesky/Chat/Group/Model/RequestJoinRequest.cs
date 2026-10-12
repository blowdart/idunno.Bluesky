// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Chat.Group.Model;

internal sealed record RequestJoinRequest
{
    public RequestJoinRequest(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        Code = code;
    }

    public string Code { get; init; }
}