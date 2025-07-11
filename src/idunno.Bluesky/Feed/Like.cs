// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using idunno.AtProto.Repo;
using idunno.Bluesky.Actor;

namespace idunno.Bluesky.Feed
{
    /// <summary>
    /// Encapsulates a like record.
    /// </summary>
    [SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "It's like in the Bluesky lexicon.")]
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public sealed record Like : AtProtoObject
    {
        [JsonConstructor]
        internal Like(DateTimeOffset createdAt, ProfileView actor)
        {
            CreatedAt = createdAt;
            Actor = actor;
        }

        /// <summary>
        /// The <see cref="DateTimeOffset"/> the post was liked.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public DateTimeOffset CreatedAt { get; init; }

        /// <summary>
        /// The <see cref="DateTimeOffset"/> the like was indexed on.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public DateTimeOffset IndexedAt { get; init; }

        /// <summary>
        /// A <see cref="ProfileView"/> of the user that liked the post.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public ProfileView Actor { get; init; }

        private string DebuggerDisplay
        {
            get
            {
                return $"Liked {Actor.Handle} on {CreatedAt.LocalDateTime:R}";
            }
        }
    }
}
