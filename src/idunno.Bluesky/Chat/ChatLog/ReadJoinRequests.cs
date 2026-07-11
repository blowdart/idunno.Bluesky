// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

#pragma warning disable IDE0130
namespace idunno.Bluesky.Chat;
#pragma warning restore IDE0130

/// <summary>
/// Event indicating the group owner marked join requests as read. Only the owner gets this.
/// </summary>
public sealed record ReadJoinRequests : LogBase
{
    /// <summary>
    /// Constructs a new instance of <see cref="ReadJoinRequests"/>.
    /// </summary>
    /// <param name="conversationId">The conversation identifier.</param>
    /// <param name="revision">The conversation revision.</param>
    [JsonConstructor]
    internal ReadJoinRequests(string conversationId, string revision) : base(conversationId, revision)
    {
    }
}