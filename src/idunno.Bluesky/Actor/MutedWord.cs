// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace idunno.Bluesky.Actor
{
    /// <summary>
    /// Properties for a word that the account owner has muted.
    /// </summary>
    public record MutedWord
    {
        /// <summary>
        /// Creates a new instance of <see cref="MutedWord"/>.
        /// </summary>
        /// <param name="id">An optional identifier for the muted word configuration.</param>
        /// <param name="value">The muted word.</param>
        /// <param name="targets">The intended targets of the muted word.</param>
        /// <param name="actorTarget">Groups of users to apply the muted word to.</param>
        /// <param name="expiresAt">The date and time at which the muted word will expire and no longer be applied, if any.</param>
        public MutedWord(string? id, string value, IReadOnlyList<MutedWordTarget> targets, MutedWordActorTarget actorTarget, DateTimeOffset? expiresAt)
        {
            Id = id;
            Value = value;
            Targets = new List<MutedWordTarget>(targets).AsReadOnly();
            ActorTarget = actorTarget;
            ExpiresAt = expiresAt;
        }

        /// <summary>
        /// An optional identifier for the muted word configuration.
        /// </summary>
        [JsonInclude]
        public string? Id { get; init; }

        /// <summary>
        /// The muted word.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public string Value { get; init; }

        /// <summary>
        /// The intended targets of the muted word.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public IReadOnlyList<MutedWordTarget> Targets { get; init; }

        /// <summary>
        /// Groups of users to apply the muted word to.
        /// </summary>
        [JsonInclude]
        public MutedWordActorTarget ActorTarget { get; init; }

        /// <summary>
        /// The date and time at which the muted word will expire and no longer be applied, if any.
        /// </summary>
        [JsonInclude]
        public DateTimeOffset? ExpiresAt { get; init; }
    }

    /// <summary>
    /// The intended targets of the muted word.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum MutedWordTarget
    {
        /// <summary>
        /// The mute word should apply to post content.
        /// </summary>
        Content,

        /// <summary>
        /// The mute word should apply to tags.
        /// </summary>
        Tag
    }

    /// <summary>
    /// Groups of users to apply the muted word to.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum MutedWordActorTarget
    {
        /// <summary>
        /// The mute word applies to all users.
        /// </summary>
        All,

        /// <summary>
        /// The mute word does not apply to users the actor is following.
        /// </summary>
        [JsonStringEnumMemberName("exclude-following")]
        ExcludeFollowing
    }
}
