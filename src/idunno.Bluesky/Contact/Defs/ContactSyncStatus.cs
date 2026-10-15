// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

#pragma warning disable IDE0130
namespace idunno.Bluesky.Contact;
#pragma warning restore IDE0130

/// <summary>
/// Describes the latest contact import and the number of remaining matches.
/// </summary>
public sealed record ContactSyncStatus
{
    [JsonConstructor]
    internal ContactSyncStatus(DateTimeOffset syncedAt, int matchesCount)
    {
        if (matchesCount < 0)
        {
            throw new JsonException("The contact match count cannot be negative.");
        }

        SyncedAt = syncedAt;
        MatchesCount = matchesCount;
    }

    /// <summary>
    /// Gets the timestamp of the last contact import.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public DateTimeOffset SyncedAt { get; internal init; }

    /// <summary>
    /// Gets the number of matches that have not been followed or dismissed.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public int MatchesCount { get; internal init; }
}
