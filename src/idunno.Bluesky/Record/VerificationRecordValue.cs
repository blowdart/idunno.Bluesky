// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;

namespace idunno.Bluesky.Record
{
    /// <summary>
    /// Encapsulates a verification record.
    /// </summary>
    public sealed record VerificationRecordValue : BlueskyRecordValue
    {
        [JsonConstructor]
        internal VerificationRecordValue(Handle handle, Did subject, DateTimeOffset createdAt, string displayName)
        {
            Handle = handle;
            Subject = subject;
            CreatedAt = createdAt;
            DisplayName = displayName;
        }

        /// <summary>
        /// Gets the <see cref="AtProto.Handle"/> of the actor the verification record refers to.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public Handle Handle { get; init; }

        /// <summary>
        /// Gets the <see cref="Did"/> of the actor the verification record refers to.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public Did Subject { get; init; }

        /// <summary>
        /// Gets <see cref="DateTimeOffset"/> the record was created at.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public DateTimeOffset CreatedAt { get; init; }

        /// <summary>
        /// Gets the display name of the actor the verification record refers to.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public string DisplayName { get; init; }
    }
}
