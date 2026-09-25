// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.Bluesky.Embed;
using idunno.Bluesky.Record;

namespace idunno.Bluesky.Actor;

/// <summary>
/// A profile status
/// </summary>
[JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = true,
                 UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToBaseType)]
[JsonDerivedType(typeof(Status), typeDiscriminator: RecordType.Status)]
public record Status : BlueskyTimestampedRecord
{
    /// <summary>
    /// Creates a new instance of <see cref="Status"/>, with the specified <paramref name="accountStatus"/> value.
    /// </summary>
    /// <param name="accountStatus">The status to set. Known values are contained in <see cref="KnownStatusValues"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="accountStatus"/> is <see langword="null"/>.</exception>
    public Status(string accountStatus) : this(
        accountStatus: accountStatus,
        embed: null,
        durationMinutes: null,
        createdAt: DateTimeOffset.UtcNow)
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="Status"/>, with the specified <paramref name="accountStatus"/> value.
    /// </summary>
    /// <param name="accountStatus">The status to set. Known values are contained in <see cref="KnownStatusValues"/>.</param>
    /// <param name="createdAt">The date and time when the status was created. Defaults to <see cref="DateTimeOffset.UtcNow"/></param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="accountStatus"/> is <see langword="null"/>.</exception>
    public Status(string accountStatus, DateTimeOffset createdAt) : this(
        accountStatus: accountStatus,
        embed: null,
        durationMinutes: null,
        createdAt: createdAt)
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="Status"/>.
    /// </summary>
    /// <param name="accountStatus">The status for the account.</param>
    /// <param name="embed">An optional embed associated with the status.</param>
    /// <param name="durationMinutes">"The duration of the status in minutes. Applications can choose to impose minimum and maximum limits.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="accountStatus"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="durationMinutes"/> is less than one.</exception>
    public Status(
        string accountStatus,
        EmbeddedBase? embed,
        int? durationMinutes) : this(
            accountStatus: accountStatus,
            embed: embed,
            durationMinutes: durationMinutes,
            createdAt: DateTimeOffset.UtcNow)
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="Status"/>.
    /// </summary>
    /// <param name="accountStatus">The status for the account.</param>
    /// <param name="embed">An optional embed associated with the status.</param>
    /// <param name="durationMinutes">"The duration of the status in minutes. Applications can choose to impose minimum and maximum limits.</param>
    /// <param name="createdAt">The date and time when the status was created. Defaults to <see cref="DateTimeOffset.UtcNow"/></param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="accountStatus"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="durationMinutes"/> is less than one.</exception>
    [JsonConstructor]
    public Status(
        string accountStatus,
        EmbeddedBase? embed,
        int? durationMinutes,
        DateTimeOffset createdAt) : base(createdAt)
    {
        AccountStatus = accountStatus;
        Embed = embed;
        DurationMinutes = durationMinutes;
    }

    /// <summary>
    /// Gets or sets the status for the account.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when the value set is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>Known values are contained in <see cref="KnownStatusValues"/>.</para>
    /// </remarks>
    [JsonPropertyName("status")]
    [JsonRequired]
    public string AccountStatus
    {
        get;

        set
        {
            ArgumentNullException.ThrowIfNull(value);

            field = value;
        }
    }

    /// <summary>
    /// Any embedded record for the status.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public EmbeddedBase? Embed { get; set; }

    /// <summary>
    /// Gets or sets the duration of the status in minutes. Applications can choose to impose minimum and maximum limits.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value set is less than one.</exception>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? DurationMinutes
    {
        get;

        set
        {
            if (value is not null)
            {
                ArgumentOutOfRangeException.ThrowIfLessThan(value.Value, 1);
            }

            field = value;
        }
    }
}

/// <summary>
/// Known values for the <see cref="Status.AccountStatus"/> property.
/// </summary>
public static class KnownStatusValues
{
    /// <summary>
    /// The user is live.
    /// </summary>
    public const string Live = "app.bsky.actor.status#live";
}