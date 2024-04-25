// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Bluesky.Actor
{
    /// <summary>
    /// Represents the detailed profile view of an actor.
    /// The information contained in the profile will vary depending on whether the profile
    /// was loaded from an authenticated or unauthenticated request.
    /// </summary>
    public sealed record ActorProfile
    {
        /// <summary>
        /// Creates a new instance of <see cref="ActorProfile"/> with the specified <paramref name="did"/> and <paramref name="handle"/>.
        /// </summary>
        /// <param name="did">The <paramref name="did"/> of the actor.</param>
        /// <param name="handle">The <paramref name="handle"/> of the actor.</param>
        [JsonConstructor]
        public ActorProfile(Did did, Handle handle)
        {
            Did = did;
            Handle = handle;
        }

        [JsonInclude]
        [JsonRequired]
        /// <summary>
        /// Gets the <see cref="Did"/> of the actor.
        /// </summary>
        /// <value>
        /// The <see cref="Did"/> of the actor.
        /// </value>
        public Did Did { get; internal set; }

        [JsonInclude]
        [JsonRequired]
        /// <summary>
        /// Gets the <see cref="Handle"/> of the actor.
        /// </summary>
        /// <value>
        /// The <see cref="Handle"/> of the actor.
        /// </value>
        public Handle Handle { get; internal set; }

        /// <summary>
        /// Gets or sets an optional description of the actor.
        /// </summary>
        /// <value>
        /// An optional description of the actor.
        /// </value>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets an optional Uri to the actor's profile avatar.
        /// </summary>
        /// <value>
        /// An optional Uri to the actor's profile avatar.
        /// </value>
        public Uri? Avatar { get; set; }

        /// <summary>
        /// Gets or sets an optional Uri to the actor's profile banner image.
        /// </summary>
        /// <value>
        /// An optional Uri to the actor's profile banner image.
        /// </value>
        public Uri? Banner { get; set; }

        /// <summary>
        /// Gets or sets the number of followers for the actor.
        /// </summary>
        /// <value>
        /// The number of followers for the actor.
        /// </value>
        public long? FollowersCount { get; set; }

        /// <summary>
        /// Gets or sets the number of accounts the actor follows.
        /// </summary>
        /// <value>
        /// The number of accounts the actor follows.
        /// </value>
        public long? FollowsCount { get; set; }

        /// <summary>
        /// Gets or sets the number of posts the actor has made.
        /// </summary>
        /// <value>
        /// The number of posts the actor has made.
        /// </value>
        public long? PostsCount { get; set; }

        /// <summary>
        /// Gets or sets the last date and time the actor was indexed.
        /// </summary>
        /// <value>
        /// The last date and time the actor was indexed.
        /// </value>
        public DateTimeOffset? IndexedAt { get; set; }

        /// <summary>
        /// Gets any labels associated with the actor.
        /// </summary>
        [JsonInclude]
        public IReadOnlyList<Label> Labels { get; internal set; } = new List<Label>();

        /// <summary>
        /// Gets any labels associated with the actor.
        /// </summary>
        public Associated? Associated { get; set; }
    }
}
