// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;

namespace idunno.Bluesky.Drafts
{
    /// <summary>
    /// Encapsulates a draft with an identifier, used to store drafts in private storage (stash).
    /// </summary>
    public record DraftWithId
    {
        /// <summary>
        /// Creates a new instance of <see cref="DraftWithId"/> with the specified identifier.
        /// </summary>
        /// <param name="id">The <see cref="TimestampIdentifier"/> the draft was created at.</param>
        /// <param name="draft">The draft.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="id"/> or <paramref name="draft"/> is null.</exception>
        [JsonConstructor]
        public DraftWithId(TimestampIdentifier id, Draft draft)
        {
            ArgumentNullException.ThrowIfNull(id);
            ArgumentNullException.ThrowIfNull(draft);

            Id = id;
            Draft = draft;
        }

        /// <summary>
        /// Creates a new instance of <see cref="DraftWithId"/> with a new unique identifier.
        /// </summary>
        /// <param name="draft">The draft.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="draft"/> is null.</exception>
        public DraftWithId(Draft draft) : this(TimestampIdentifier.Next(), draft)
        {
            ArgumentNullException.ThrowIfNull(draft);
        }

        /// <summary>
        /// Gets the <see cref="TimestampIdentifier"/> used as a draft identifier.
        /// </summary>
        [JsonRequired]
        public TimestampIdentifier Id { get; init; }

        /// <summary>
        /// Gets the draft containing an array of draft posts.
        /// </summary>
        [JsonRequired]
        public Draft Draft { get; init; }
    }
}
