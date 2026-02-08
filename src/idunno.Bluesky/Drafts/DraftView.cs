// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;

namespace idunno.Bluesky.Drafts
{
    /// <summary>
    /// View to present drafts data to users.
    /// </summary>
    public sealed record DraftView : View
    {
        /// <summary>
        /// Creates a new instance of <see cref="DraftView"/> with the specified identifier, draft, creation time, and update time.
        /// </summary>
        /// <param name="id">The <see cref="TimestampIdentifier"/> used as a draft identifier.</param>
        /// <param name="draft">The draft containing an array of draft posts and metadata.</param>
        /// <param name="createdAt">The time the draft was created.</param>
        /// <param name="updatedAt">The time the draft was last updated.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="id"/> or <paramref name="draft"/> is null.</exception>
        [JsonConstructor]
        internal DraftView(
            TimestampIdentifier id,
            Draft draft,
            DateTimeOffset createdAt,
            DateTimeOffset updatedAt)
        {
            ArgumentNullException.ThrowIfNull(id);
            ArgumentNullException.ThrowIfNull(draft);
            Id = id;
            Draft = draft;
            CreatedAt = createdAt;
            UpdatedAt = updatedAt;
        }

        /// <summary>
        /// Gets the <see cref="TimestampIdentifier"/> used as a draft identifier.
        /// </summary>
        [JsonRequired]
        public TimestampIdentifier Id { get; init; }

        /// <summary>
        /// Gets the draft containing an array of draft posts and metadata.
        /// </summary>
        [JsonRequired]
        public Draft Draft { get; init; }

        /// <summary>
        /// Gets the time the draft was created.
        /// </summary>
        [JsonRequired]
        public DateTimeOffset CreatedAt { get; init; }

        /// <summary>
        /// Gets the time the draft was last updated.
        /// </summary>
        [JsonRequired]
        public DateTimeOffset UpdatedAt { get; init; }
    }
}
