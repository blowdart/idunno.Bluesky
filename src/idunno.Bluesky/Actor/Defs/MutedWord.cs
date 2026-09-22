// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Actor;
#pragma warning restore IDE0130 // Namespace does not match folder structure

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
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> or <paramref name="targets"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="value"/> is longer than <see cref="Maximum.MutedWordLengthInBytes"/> bytes or
    /// <see cref="Maximum.MutedWordLengthInGraphemes"/> graphemes, when <paramref name="actorTarget"/> is
    /// <see cref="MutedWordActorTarget.Unknown"/>, or when <paramref name="targets"/> contains
    /// <see cref="MutedWordTarget.Unknown"/>.
    /// </exception>
    public MutedWord(string? id, string value, IReadOnlyList<MutedWordTarget> targets, MutedWordActorTarget actorTarget, DateTimeOffset? expiresAt)
        : this(
            id,
            value,
            ToTargetValues(targets),
            ActorWireValues.FromMutedWordActorTarget(actorTarget, nameof(actorTarget)),
            expiresAt)
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="MutedWord"/> from the values the service sent.
    /// </summary>
    /// <param name="id">An optional identifier for the muted word configuration.</param>
    /// <param name="value">The muted word.</param>
    /// <param name="targetValues">The intended targets of the muted word, as the service expresses them.</param>
    /// <param name="actorTargetValue">Groups of users to apply the muted word to, as the service expresses them.</param>
    /// <param name="expiresAt">The date and time at which the muted word will expire and no longer be applied, if any.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> or <paramref name="targetValues"/> is <see langword="null"/>.</exception>
    [JsonConstructor]
    internal MutedWord(string? id, string value, IReadOnlyList<string> targetValues, string? actorTargetValue, DateTimeOffset? expiresAt)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(targetValues);

        Id = id;
        Value = value;
        TargetValues = targetValues;
        ActorTargetValue = actorTargetValue;
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
    /// <exception cref="ArgumentNullException">Thrown when the value set is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the value set is longer than <see cref="Maximum.MutedWordLengthInBytes"/> bytes or
    /// <see cref="Maximum.MutedWordLengthInGraphemes"/> graphemes.
    /// </exception>
    [JsonInclude]
    [JsonRequired]
    public string Value
    {
        get;

        init
        {
            ArgumentNullException.ThrowIfNull(value);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value.GetUtf8Length(), Maximum.MutedWordLengthInBytes);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value.GetGraphemeLength(), Maximum.MutedWordLengthInGraphemes);

            field = value;
        }
    }

    /// <summary>
    /// Gets the intended targets of the muted word, as the service expresses them.
    /// </summary>
    /// <remarks>
    /// <para>A muted word target is an open union, so the values the service sent are kept verbatim and
    /// <see cref="Targets"/> is projected from them. A target this library does not recognize survives a read,
    /// modify and write cycle rather than being discarded.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when the value set is <see langword="null"/>.</exception>
    [JsonInclude]
    [JsonRequired]
    [JsonPropertyName("targets")]
    internal IReadOnlyList<string> TargetValues
    {
        get;

        init
        {
            ArgumentNullException.ThrowIfNull(value);

            field = new List<string>(value).AsReadOnly();
        }
    }

    /// <summary>
    /// The intended targets of the muted word.
    /// </summary>
    /// <remarks>
    /// <para>A target this library does not recognize is reported as <see cref="MutedWordTarget.Unknown"/>.</para>
    /// </remarks>
    [JsonIgnore]
    public IReadOnlyList<MutedWordTarget> Targets =>
        [.. TargetValues.Select(ActorWireValues.ToMutedWordTarget)];

    /// <summary>
    /// Gets the groups of users to apply the muted word to, as the service expresses them.
    /// </summary>
    /// <remarks>
    /// <para>A muted word actor target is an open union, so the value the service sent is kept verbatim and
    /// <see cref="ActorTarget"/> is projected from it. A target this library does not recognize survives a read,
    /// modify and write cycle rather than being discarded.</para>
    /// </remarks>
    [JsonInclude]
    [JsonPropertyName("actorTarget")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    internal string? ActorTargetValue { get; init; }

    /// <summary>
    /// Groups of users to apply the muted word to.
    /// </summary>
    /// <remarks>
    /// <para>A target this library does not recognize is reported as <see cref="MutedWordActorTarget.Unknown"/>.
    /// When the service does not send a target the lexicon default, <see cref="MutedWordActorTarget.All"/>,
    /// is reported.</para>
    /// </remarks>
    [JsonIgnore]
    public MutedWordActorTarget ActorTarget =>
        ActorTargetValue is null ? MutedWordActorTarget.All : ActorWireValues.ToMutedWordActorTarget(ActorTargetValue);

    /// <summary>
    /// The date and time at which the muted word will expire and no longer be applied, if any.
    /// </summary>
    [JsonInclude]
    public DateTimeOffset? ExpiresAt { get; init; }

    private static IReadOnlyList<string> ToTargetValues(IReadOnlyList<MutedWordTarget> targets)
    {
        ArgumentNullException.ThrowIfNull(targets);

        return [.. targets.Select(target => ActorWireValues.FromMutedWordTarget(target, nameof(targets)))];
    }
}

/// <summary>
/// The intended targets of the muted word.
/// </summary>
public enum MutedWordTarget
{
    /// <summary>
    /// The mute word should apply to post content.
    /// </summary>
    Content,

    /// <summary>
    /// The mute word should apply to tags.
    /// </summary>
    Tag,

    /// <summary>
    /// The target is one this library does not recognize.
    /// </summary>
    /// <remarks>
    /// <para>This value only ever comes from the service. It cannot be used to build a <see cref="MutedWord"/>,
    /// as it carries no target the service would understand.</para>
    /// </remarks>
    Unknown
}

/// <summary>
/// Groups of users to apply the muted word to.
/// </summary>
public enum MutedWordActorTarget
{
    /// <summary>
    /// The mute word applies to all users.
    /// </summary>
    All,

    /// <summary>
    /// The mute word does not apply to users the actor is following.
    /// </summary>
    ExcludeFollowing,

    /// <summary>
    /// The target is one this library does not recognize.
    /// </summary>
    /// <remarks>
    /// <para>This value only ever comes from the service. It cannot be used to build a <see cref="MutedWord"/>,
    /// as it carries no target the service would understand.</para>
    /// </remarks>
    Unknown
}
