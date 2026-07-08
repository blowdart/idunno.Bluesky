// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Chat;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// System message indicating a user was added to the group conversation.
/// </summary>
public sealed record EditGroupSystemMessage : SystemMessage
{
    [JsonConstructor]
    internal EditGroupSystemMessage(string oldName, string newName)
    {
        OldName = oldName;
        NewName = newName;
    }

    /// <summary>
    /// Gets the old name of the group conversation.
    /// </summary>
    [JsonRequired]
    public string OldName { get; init; }

    /// <summary>
    /// Gets the new name of the group conversation.
    /// </summary>
    [JsonRequired]
    public string NewName { get; init; }
}