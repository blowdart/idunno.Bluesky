// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using idunno.AtProto.Repo;

namespace idunno.Bluesky.Actions.Model
{
    /// <summary>
    /// Encapsulates the information needed to create a repost record.
    /// </summary>
    public sealed record NewRepostRecord
    {
        /// <summary>
        /// Creates a new instance of <see cref="NewRepostRecord"/>.
        /// </summary>
        /// <param name="subject">The <see cref="StrongReference"/> to the post to be reposted.</param>
        /// <param name="createdAt">An optional <see cref="DateTimeOffset"/> for the repost creation date, defaults to now.</param>
        public NewRepostRecord(StrongReference subject, DateTimeOffset? createdAt = null)
        {
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
        /// The record type for a repost record.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("$type")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Cannot be static as it won't be serialized.")]
        public string Type => RecordType.Repost;

        /// <summary>
        /// Gets the <see cref="StrongReference"/> to the post to be reposted.
        /// </summary>
        [JsonInclude]
        public StrongReference Subject { get; init; }

        /// <summary>
        /// Gets <see cref="DateTimeOffset"/> the repost record was created.
        /// </summary>
        [JsonInclude]
        public DateTimeOffset CreatedAt { get; init; }
    }
}
