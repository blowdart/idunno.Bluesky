﻿// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using idunno.AtProto.Repo;
using idunno.Bluesky.Record;

namespace idunno.Bluesky.Actions
{
    /// <summary>
    /// Encapsulates the information needed to create a like record.
    /// </summary>
    [SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "It's like in the Bluesky lexicon.")]
    public sealed record Like : BlueskyTimestampedRecord
    {
        /// <summary>
        /// Creates a new instance of <see cref="Like"/> with <see cref="BlueskyTimestampedRecord.CreatedAt"/> set to the current date and time.
        /// </summary>
        /// <param name="subject">The <see cref="StrongReference"/> to the post to be liked.</param>
        public Like(StrongReference subject) : this(subject, DateTimeOffset.UtcNow)
        {
        }

        /// <summary>
        /// Creates a new instance of <see cref="Like"/>.
        /// </summary>
        /// <param name="subject">The <see cref="StrongReference"/> to the post to be liked.</param>
        /// <param name="createdAt">The <see cref="DateTimeOffset"/> for the repost creation date.</param>
        [JsonConstructor]
        public Like(StrongReference subject, DateTimeOffset createdAt) : base(createdAt)
        {
            Subject = subject;
        }


        /// <summary>
        /// Gets the <see cref="StrongReference"/> to the record to be reposted.
        /// </summary>
        [JsonInclude]
        public StrongReference Subject { get; init; }
    }
}
