// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto.Repo;
using idunno.Bluesky.Record;

namespace idunno.Bluesky.Actions
{
    /// <summary>
    /// Encapsulates the information needed to create a like record.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "Matching Bluesky naming.")]
    public sealed record LikeRecordValue : BlueskyRecordValue
    {
        /// <summary>
        /// Creates a new instance of <see cref="LikeRecordValue"/> with <see cref="BlueskyRecordValue.CreatedAt"/> set to the current date and time.
        /// </summary>
        /// <param name="subject">The <see cref="StrongReference"/> to the post to be liked.</param>
        public LikeRecordValue(StrongReference subject) : this(subject, DateTimeOffset.UtcNow)
        {
        }

        /// <summary>
        /// Creates a new instance of <see cref="LikeRecordValue"/>.
        /// </summary>
        /// <param name="subject">The <see cref="StrongReference"/> to the post to be liked.</param>
        /// <param name="createdAt">An optional <see cref="DateTimeOffset"/> for the repost creation date, defaults to now.</param>
        [JsonConstructor]
        public LikeRecordValue(StrongReference subject, DateTimeOffset? createdAt) : base(createdAt)
        {
            Subject = subject;
        }

        /// <summary>
        /// The record type for a repost record.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("$type")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Cannot be static as it won't be serialized.")]
        public string Type => RecordType.Like;

        /// <summary>
        /// Gets the <see cref="StrongReference"/> to the record to be reposted.
        /// </summary>
        [JsonInclude]
        public StrongReference Subject { get; init; }
    }
}
