// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.AtProto.Labels;

namespace idunno.Bluesky.Actor
{
    /// <summary>
    /// Encapsulates a view over a user profile.
    /// </summary>
    public record ProfileView : ProfileViewBasic
    {
        /// <summary>
        /// Creates a new instance of <see cref="ProfileView"/>
        /// </summary>
        /// <param name="did">The <see cref="Did"/> of the actor.</param>
        /// <param name="handle">The <see cref="Handle"/> of the actor.</param>
        /// <param name="displayName">The display name for the actor.</param>
        /// <param name="description">The description for the actor.</param>
        /// <param name="pronouns">The actor's pronouns, if any.</param>
        /// <param name="website">The actor's website, if any.</param>
        /// <param name="avatar">A <see cref="Uri"/> to the actor's avatar, if any.</param>
        /// <param name="associated">Properties associated with the actor.</param>
        /// <param name="viewer">Metadata about the current user's relationship to the actor.</param>
        /// <param name="labels">Labels applied to the actor.</param>
        /// <param name="indexedAt">The date and time the actor was last indexed.</param>
        /// <param name="createdAt">The date and time the actor was created.</param>
        /// <param name="verification">The <see cref="VerificationState"/> of the actor, if any.</param>
        /// <param name="status">The <see cref="StatusView"/> of the actor, if any.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="did"/> or <paramref name="handle"/> are null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        ///   Thrown when <paramref name="displayName"/> is not null and has a character length greater than 640 or a grapheme length greater than 64.
        /// </exception>
        [JsonConstructor]
        public ProfileView(
            Did did,
            Handle handle,
            string? displayName,
            string? description,
            string? pronouns,
            Uri? website,
            Uri? avatar,
            ProfileAssociated? associated,
            ActorViewerState? viewer,
            IReadOnlyCollection<Label>? labels,
            DateTimeOffset? indexedAt,
            DateTimeOffset? createdAt,
            VerificationState? verification,
            StatusView? status = null
            ) : base(did, handle, displayName, pronouns, website, avatar, associated, viewer, labels, createdAt, verification, status)
        {
            Description = description;
            IndexedAt = indexedAt;
            Status = status;
        }

        /// <summary>
        /// Gets the actor's profile description
        /// </summary>
        public string? Description { get; init; }

        /// <summary>
        /// Gets the date and time the actor was last indexed.
        /// </summary>
        public DateTimeOffset? IndexedAt { get; init; }

        /// <summary>
        /// Gets a string representation of the <see cref="ProfileView"/>.
        /// This returns the actor's display name, if any, otherwise returns the actor's handle.
        /// </summary>
        /// <returns>A string representation of the <see cref="ProfileView"/>.</returns>
        public override string ToString()
        {
            if (!string.IsNullOrWhiteSpace(DisplayName))
            {
                return $"{DisplayName} ({Handle})";
            }
            else
            {
                return $"{Handle}";
            }
        }
    }
}
