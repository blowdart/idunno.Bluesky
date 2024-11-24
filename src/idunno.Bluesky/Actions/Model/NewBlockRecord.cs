// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;

namespace idunno.Bluesky.Actions.Model
{
    /// <summary>
    /// Encapsulates the information needed to create a block record.
    /// </summary>
    public sealed record NewBlockRecord
    {
        /// <summary>
        /// Creates a new instance of <see cref="NewBlockRecord"/>.
        /// </summary>
        /// <param name="subject">The <see cref="Did"/> to the actor to be blocked.</param>
        /// <param name="createdAt">An optional <see cref="DateTimeOffset"/> for the repost creation date, defaults to now.</param>
        public NewBlockRecord(Did subject, DateTimeOffset? createdAt = null)
        {
            ArgumentNullException.ThrowIfNull(subject);

            Subject = subject;

            if (createdAt is not null)
            {
                CreatedAt = (DateTimeOffset)createdAt;
            }
            else
            {
                CreatedAt = DateTimeOffset.UtcNow;
            }
        }

        /// <summary>
        /// The record type for a block record.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("$type")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Cannot be static as it won't be serialized.")]
        public string Type => RecordType.Block;

        /// <summary>
        /// Gets the <see cref="Did"/> of the subject to be blocked.
        /// </summary>
        [JsonInclude]
        public Did Subject { get; init; }

        /// <summary>
        /// Gets <see cref="DateTimeOffset"/> the block record was created.
        /// </summary>
        [JsonInclude]
        public DateTimeOffset CreatedAt { get; init; }
    }
}
