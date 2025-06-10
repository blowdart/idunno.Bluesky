// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using idunno.Bluesky.Embed;

namespace idunno.Bluesky.Record
{
    /// <summary>
    /// Creates a new instance of <see cref="Status"/>.
    /// </summary>
    /// <param name="AccountStatus">The status for the account.</param>
    /// <param name="Embed">An optional embed associated with the status.</param>
    /// <param name="DurationMinutes">"The duration of the status in minutes. Applications can choose to impose minimum and maximum limits.</param>
    public record Status(
        [property: JsonPropertyName("status")] string AccountStatus,
        EmbeddedBase? Embed,
        int? DurationMinutes) : BlueskyRecord
    {
    }
}
