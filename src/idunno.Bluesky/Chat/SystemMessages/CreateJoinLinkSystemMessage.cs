// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Chat;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// System message indicating the group join link was created.
/// </summary>
public sealed record CreateJoinLinkSystemMessage : SystemMessage
{
    [JsonConstructor]
    internal CreateJoinLinkSystemMessage()
    {
    }
}
