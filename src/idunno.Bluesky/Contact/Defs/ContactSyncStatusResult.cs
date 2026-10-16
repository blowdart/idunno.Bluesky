// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

#pragma warning disable IDE0130
namespace idunno.Bluesky.Contact;
#pragma warning restore IDE0130

/// <summary>
/// Contains the optional synchronization status for contact discovery.
/// </summary>
public sealed class ContactSyncStatusResult
{
    [JsonConstructor]
    internal ContactSyncStatusResult(ContactSyncStatus? syncStatus)
    {
        SyncStatus = syncStatus;
    }

    /// <summary>
    /// Gets the synchronization status, or <see langword="null"/> when contacts have never been imported or their data was removed.
    /// </summary>
    public ContactSyncStatus? SyncStatus { get; }
}
