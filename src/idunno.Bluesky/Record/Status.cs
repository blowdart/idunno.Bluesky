// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using idunno.Bluesky.Embed;

namespace idunno.Bluesky.Record
{
    /// <summary>
    /// A profile status
    /// </summary>
    public record Status :  BlueskyTimestampedRecord
    {
        /// <summary>
        /// Creates a new instance of <see cref="Status"/>.
        /// </summary>
        /// <param name="status">The status for the account.</param>
        /// <param name="embed">An optional embed associated with the status.</param>
        /// <param name="durationMinutes">"The duration of the status in minutes. Applications can choose to impose minimum and maximum limits.</param>
        /// <param name="createdAt">The date and time when the status was created. Defaults to <see cref="DateTimeOffset.UtcNow"/></param>
        public Status(
            string status = "app.bsky.actor.status#live",
            EmbeddedBase? embed = null,
            int? durationMinutes = null,
            DateTimeOffset? createdAt = null) : base(createdAt)
        {
            AccountStatus = status;
            Embed = embed;
            DurationMinutes = durationMinutes;
        }

        /// <summary>
        /// Gets the status for the account.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("status")]
        public string AccountStatus { get; init; } = "app.bsky.actor.status#live";

        /// <summary>
        /// Any embedded record for the status.
        /// </summary>
        public EmbeddedBase? Embed { get; init; }

        /// <summary>
        /// Gets the duration of the status in minutes. Applications can choose to impose minimum and maximum limits.
        /// </summary>
        [JsonInclude]
        public int? DurationMinutes { get; init; }
    }
}
