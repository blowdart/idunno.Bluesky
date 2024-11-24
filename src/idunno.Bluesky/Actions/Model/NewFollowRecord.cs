// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;

namespace idunno.Bluesky.Actions.Model
{
    /// <summary>
    /// Encapsulates the information needed to create a follow record.
    /// </summary>
    public sealed record NewFollowRecord
    {
        /// <summary>
        /// Creates a new instance of <see cref="NewBlockRecord"/>.
        /// </summary>
        /// <param name="subject">The <see cref="Did"/> to the actor to be followed.</param>
        /// <param name="createdAt">An optional <see cref="DateTimeOffset"/> for the repost creation date, defaults to now.</param>
        [JsonConstructor]
        public NewFollowRecord(Did subject, DateTimeOffset? createdAt=null)
        {
            ArgumentNullException.ThrowIfNull(subject);
            Subject = subject;
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow;
        }

        /// <summary>
        /// The record type for a follow record.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("$type")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Cannot be static as it won't be serialized.")]
        public string Type => RecordType.Follow;

        /// <summary>
        /// Gets the <see cref="Did"/> to the actor to follow.
        /// </summary>
        [JsonInclude]
        public Did Subject { get; init; }

        /// <summary>
        /// Gets <see cref="DateTimeOffset"/> the follow record was created.
        /// </summary>
        [JsonInclude]
        public DateTimeOffset CreatedAt { get; init; }
    }
}
