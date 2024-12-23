// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using idunno.AtProto.Repo;
using idunno.Bluesky.Record;

namespace idunno.Bluesky.Actions
{
    /// <summary>
    /// Encapsulates the information needed to create a repost record.
    /// </summary>
    public sealed record RepostRecordValue : BlueskyTimestampedRecordValue
    {
        /// <summary>
        /// Creates a new instance of <see cref="RepostRecordValue"/> with<see cref = "BlueskyTimestampedRecordValue.CreatedAt" /> set to the current date and time.
        /// </summary>
        /// <param name="subject">The <see cref="StrongReference"/> to the post to be reposted.</param>
        public RepostRecordValue(StrongReference subject) : this(subject, DateTimeOffset.UtcNow)
        {
        }

        /// <summary>
        /// Creates a new instance of <see cref="RepostRecordValue"/>.
        /// </summary>
        /// <param name="subject">The <see cref="StrongReference"/> to the post to be reposted.</param>
        /// <param name="createdAt">The <see cref="DateTimeOffset"/> for the repost creation date.</param>
        [JsonConstructor]
        public RepostRecordValue(StrongReference subject, DateTimeOffset createdAt) : base(createdAt)
        {
            Subject = subject;
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
    }
}
