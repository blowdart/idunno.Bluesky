// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.AtProto.Labels;

namespace idunno.Bluesky.Actor
{
    /// <summary>
    /// A basic view of an actor profile
    /// </summary>
    /// <remarks>
    /// <para>See <see href="https://github.com/bluesky-social/atproto/blob/main/lexicons/app/bsky/actor/defs.json" /> for the definition.</para>
    /// </remarks>
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public record ProfileViewBasic : View
    {
        /// <summary>
        /// Creates a new instance of <see cref="ProfileViewBasic"/>
        /// </summary>
        /// <param name="did">The <see cref="Did"/> of the actor.</param>
        /// <param name="handle">The <see cref="Handle"/> of the actor.</param>
        /// <param name="displayName">The display name for the actor.</param>
        /// <param name="pronouns">The actor's pronouns, if any.</param>
        /// <param name="website">The actor's website, if any.</param>
        /// <param name="avatar">A <see cref="Uri"/> to the actor's avatar, if any.</param>
        /// <param name="associated">Properties associated with the actor.</param>
        /// <param name="viewer">Metadata about the current user's relationship to the actor.</param>
        /// <param name="labels">Labels applied to the actor.</param>
        /// <param name="createdAt">The date and time the actor was created.</param>
        /// <param name="verification">The <see cref="VerificationState"/> of the actor, if any.</param>
        /// <param name="status">The <see cref="StatusView"/> of the actor, if any.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="did"/> or <paramref name="handle"/> are <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        ///   Thrown when <paramref name="displayName"/> is not <see langword="null"/> and has a character length greater than 640 or a grapheme length greater than 64.
        /// </exception>
        [JsonConstructor]
        public ProfileViewBasic(
            Did did,
            Handle handle,
            string? displayName,
            string? pronouns,
            Uri? website,
            Uri? avatar,
            ProfileAssociated? associated,
            ActorViewerState? viewer,
            IReadOnlyCollection<Label>? labels,
            DateTimeOffset? createdAt,
            VerificationState? verification,
            StatusView? status)
        {
            ArgumentNullException.ThrowIfNull(did);
            ArgumentNullException.ThrowIfNull(handle);

            if (displayName is not null)
            {
                ArgumentOutOfRangeException.ThrowIfGreaterThan(displayName.Length, 640);
                ArgumentOutOfRangeException.ThrowIfGreaterThan(displayName.GetGraphemeLength(), 64);
            }

            Did = did;
            Handle = handle;
            DisplayName = displayName;
            Pronouns = pronouns;
            Website = website;
            Avatar = avatar;
            Associated = associated;
            Viewer = viewer;
            CreatedAt = createdAt;
            Verification = verification;
            Status = status;

            if (labels is null)
            {
                Labels = new List<Label>().AsReadOnly();
            }
            else
            {
                Labels = new List<Label>(labels).AsReadOnly();
            }
        }

        /// <summary>
        /// Gets the did of the actor.
        /// </summary>
        [JsonRequired]
        [NotNull]
        public Did Did { get; init; }

        /// <summary>
        /// Gets the handle of the actor.
        /// </summary>
        [JsonRequired]
        [NotNull]
        public Handle Handle { get; init; }

        /// <summary>
        /// Gets the display name, if any, of the actor.
        /// </summary>
        public string? DisplayName { get; init; }

        /// <summary>
        /// Gets the actor's pronouns, if any.
        /// </summary>
        public string? Pronouns { get; init; }

        /// <summary>
        /// Gets the actor's website, if any
        /// </summary>
        public Uri? Website { get; init; }

        /// <summary>
        /// Gets the <see cref="Uri"/> of the actor's avatar, if any.
        /// </summary>
        public Uri? Avatar { get; init; }

        /// <summary>
        /// Gets the <see cref="ProfileAssociated">Associated properties</see> for the subject.
        /// </summary>
        public ProfileAssociated? Associated {get; init;}

        /// <summary>
        /// Gets metadata about the requesting account's relationship with the subject account. Only has meaningful content for authenticated requests.
        /// </summary>
        public ActorViewerState? Viewer { get; init; }

        /// <summary>
        /// Gets any labels applied to the actor.
        /// </summary>
        /// <remarks>
        ///<para>
        /// Labels will only be populated if a list of subscribed labelers from a user's <see cref="Preferences"/>
        /// is passed into an API which returns this class.
        ///</para>
        /// </remarks>
        public IReadOnlyCollection<Label> Labels { get; init; }

        /// <summary>
        /// Gets the date and time the actor was created at.
        /// </summary>
        public DateTimeOffset? CreatedAt { get; init; }

        /// <summary>
        /// Gets the <see cref="VerificationState"/> of the actor, if any.
        /// </summary>
        public VerificationState? Verification { get; init; }

        /// <summary>
        /// Gets the <see cref="StatusView" /> of the actor, if any.
        /// </summary>
        public StatusView? Status { get; init; }

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

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay
        {
            get
            {
                return $"{{{Handle} ({Did})}}";
            }
        }
    }
}
