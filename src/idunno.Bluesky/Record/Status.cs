// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.Bluesky.Embed;

namespace idunno.Bluesky.Record
{
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
            ArgumentNullException.ThrowIfNull(accountStatus);
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
            ArgumentNullException.ThrowIfNull(accountStatus);
        }

        /// <summary>
        /// Creates a new instance of <see cref="Status"/>.
        /// </summary>
        /// <param name="accountStatus">The status for the account.</param>
        /// <param name="embed">An optional embed associated with the status.</param>
        /// <param name="durationMinutes">"The duration of the status in minutes. Applications can choose to impose minimum and maximum limits.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="accountStatus"/> is <see langword="null"/>.</exception>
        public Status(
            string accountStatus,
            EmbeddedBase? embed,
            int? durationMinutes) : this (
                accountStatus: accountStatus,
                embed: embed,
                durationMinutes: durationMinutes,
                createdAt: DateTimeOffset.UtcNow)
        {
            ArgumentNullException.ThrowIfNull(accountStatus);

            AccountStatus = accountStatus;
            Embed = embed;
            DurationMinutes = durationMinutes;
        }


        /// <summary>
        /// Creates a new instance of <see cref="Status"/>.
        /// </summary>
        /// <param name="accountStatus">The status for the account.</param>
        /// <param name="embed">An optional embed associated with the status.</param>
        /// <param name="durationMinutes">"The duration of the status in minutes. Applications can choose to impose minimum and maximum limits.</param>
        /// <param name="createdAt">The date and time when the status was created. Defaults to <see cref="DateTimeOffset.UtcNow"/></param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="accountStatus"/> is <see langword="null"/>.</exception>
        [JsonConstructor]
        public Status(
            string accountStatus,
            EmbeddedBase? embed,
            int? durationMinutes,
            DateTimeOffset createdAt) : base(createdAt)
        {
            ArgumentNullException.ThrowIfNull(accountStatus);

            AccountStatus = accountStatus;
            Embed = embed;
            DurationMinutes = durationMinutes;
        }

        /// <summary>
        /// Gets the status for the account.
        /// </summary>
        /// <remakes>
        /// <para>Known values are contained in <see cref="KnownStatusValues"/>.</para>
        /// </remakes>
        [JsonPropertyName("status")]
        [JsonRequired]
        public string AccountStatus { get; set; }

        /// <summary>
        /// Any embedded record for the status.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public EmbeddedBase? Embed { get; set; }

        /// <summary>
        /// Gets the duration of the status in minutes. Applications can choose to impose minimum and maximum limits.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? DurationMinutes { get; set; }
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
}
